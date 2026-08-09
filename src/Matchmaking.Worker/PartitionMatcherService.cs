using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Matchmaking.Shared.Models;

namespace Matchmaking.Worker;

public class PartitionMatcherService : BackgroundService
{
    private readonly ILogger<PartitionMatcherService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _storageUrl;
    private readonly string _gameServerUrl;
    private readonly bool _backfillEnabled;

    private const int DefaultGameCapacity = 2;

    private readonly Dictionary<string, int> _gameCapacities = new(StringComparer.OrdinalIgnoreCase)
    {
        { "1v1", 2 },
        { "battle-royale", 60 },
        { "standard", 4 }
    };

    public PartitionMatcherService(
        ILogger<PartitionMatcherService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _storageUrl = configuration["StorageService:Url"] ?? "http://localhost:5010";
        _gameServerUrl = configuration["GameServerService:Url"] ?? "http://localhost:5020";
        _backfillEnabled = configuration.GetValue("BackfillEnabled", true);
    }

    private int GetGameCapacity(string gameId)
    {
        if (_gameCapacities.TryGetValue(gameId, out var capacity))
        {
            return capacity;
        }
        return DefaultGameCapacity;
    }

    public int GetLatencyThreshold(TimeSpan waitTime)
    {
        const int baseThreshold = 30; // Starts at 30ms tolerance
        const int maxThreshold = 200; // Cap at 200ms
        const double growRate = 10.0; // Grows by 10ms
        const double growIntervalSeconds = 5.0; // Every 5 seconds waited

        double intervals = waitTime.TotalSeconds / growIntervalSeconds;
        int wideningValue = (int)(Math.Floor(intervals) * growRate);
        return Math.Min(baseThreshold + wideningValue, maxThreshold);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PartitionMatcherService started. Running sweep loop every 1000ms. BackfillEnabled: {BackfillEnabled}", _backfillEnabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMatchmakingSweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during matchmaking sweep.");
            }

            // Perform matchmaking sweep every 1 second
            await Task.Delay(TimeSpan.FromMilliseconds(1000), stoppingToken);
        }
    }

    private async Task RunMatchmakingSweepAsync(CancellationToken stoppingToken)
    {
        // 1. Fetch all currently queued tickets from storage
        var response = await _httpClient.GetAsync($"{_storageUrl}/tickets?status=Queued", stoppingToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Sweep: Failed to retrieve queued tickets from storage. Status code: {StatusCode}", response.StatusCode);
            return;
        }

        var tickets = await response.Content.ReadFromJsonAsync<List<Ticket>>(cancellationToken: stoppingToken);
        if (tickets == null || tickets.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Sweep: Processing {Count} queued tickets...", tickets.Count);

        // Track tickets matched during this single tick to prevent double allocation
        var matchedTicketIds = new HashSet<string>();

        // 2. Discover active partitions (gameId, targetRegion) from queued tickets
        // ("1v1", "us-east")
        // ("1v1", "eu-west")
        // ("battle-royale", "eu-west")
        var partitions = tickets
            .Where(t => !string.IsNullOrEmpty(t.TargetRegion))
            .Select(t => (t.GameId, TargetRegion: t.TargetRegion))
            .Distinct()
            .ToList();

        foreach (var partition in partitions)
        {
            if (stoppingToken.IsCancellationRequested) break;

            // Get queued candidates for this specific partition
            var candidates = tickets
                .Where(t => t.GameId == partition.GameId && 
                            t.Status == TicketStatus.Queued && 
                            t.TargetRegion == partition.TargetRegion &&
                            !matchedTicketIds.Contains(t.Id))
                .OrderBy(t => t.CreatedAt) // Oldest first (fairness)
                .ToList();

            if (candidates.Count == 0) continue;

            var capacity = GetGameCapacity(partition.GameId);

            foreach (var ticket in candidates)
            {
                if (matchedTicketIds.Contains(ticket.Id)) continue;

                var waitTime = DateTimeOffset.UtcNow - ticket.CreatedAt;
                var latencyThreshold = GetLatencyThreshold(waitTime);

                // Find eligible peers in this partition
                var eligible = candidates
                    .Where(other => other.Id != ticket.Id &&
                                    !matchedTicketIds.Contains(other.Id) &&
                                    Math.Abs(other.TargetLatency - ticket.TargetLatency) <= latencyThreshold)
                    .ToList();

                // Check if we can form a new session
                if (eligible.Count + 1 >= capacity)
                {
                    // Select the oldest eligible peers first to prevent starvation
                    var group = eligible
                        .OrderBy(o => o.CreatedAt)
                        .Take(capacity - 1)
                        .Concat(new[] { ticket })
                        .ToList();

                    await CreateAndAssignSessionAsync(group, partition.GameId, partition.TargetRegion, stoppingToken);

                    foreach (var matchedTicket in group)
                    {
                        matchedTicketIds.Add(matchedTicket.Id);
                    }
                }
                else if (_backfillEnabled)
                {
                    // Look for an existing session with an open slot
                    var openSession = await FindSessionWithOpenSlotAsync(partition.GameId, partition.TargetRegion, latencyThreshold, ticket, stoppingToken);
                    if (openSession != null)
                    {
                        await AssignToExistingSessionAsync(ticket, openSession, partition.TargetRegion, stoppingToken);
                        matchedTicketIds.Add(ticket.Id);
                    }
                }
            }
        }
    }

    private async Task CreateAndAssignSessionAsync(List<Ticket> group, string gameId, string region, CancellationToken stoppingToken)
    {
        var averageLatency = (int)group.Average(t => t.TargetLatency);

        _logger.LogInformation("Sweep: Group found for game '{GameId}' in region '{Region}' (Ref Latency: {Latency}ms). Spawning session...", 
            gameId, region, averageLatency);

        // 1. Create the session on the GameServer
        var createRequest = new SessionRequest
        {
            GameId = gameId,
            Region = region,
            Capacity = GetGameCapacity(gameId),
            PlayerIds = group.Select(t => t.PlayerId).ToList(),
            ReferenceLatency = averageLatency
        };

        var response = await _httpClient.PostAsJsonAsync($"{_gameServerUrl}/sessions", createRequest, stoppingToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Sweep: Failed to create session on GameServer. Status: {StatusCode}", response.StatusCode);
            return;
        }

        var session = await response.Content.ReadFromJsonAsync<GameSession>(cancellationToken: stoppingToken);
        if (session == null)
        {
            _logger.LogError("Sweep: Failed to parse created session response from GameServer.");
            return;
        }

        // 2. Mark all tickets as matched in Storage
        foreach (var ticket in group)
        {
            var updateRequest = new UpdateStatusRequest
            {
                Status = TicketStatus.Matched,
                MatchedSessionId = session.Id,
                MatchedRegion = region
            };

            var updateResponse = await _httpClient.PutAsJsonAsync($"{_storageUrl}/tickets/{ticket.Id}/status", updateRequest, stoppingToken);
            if (!updateResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Sweep: Failed to update ticket status in storage for Ticket '{TicketId}'.", ticket.Id);
            }
        }

        _logger.LogInformation("Sweep: Successfully created and assigned session '{SessionId}' for {Count} players.", session.Id, group.Count);
    }

    private async Task<GameSession?> FindSessionWithOpenSlotAsync(
        string gameId, 
        string region, 
        int latencyThreshold, 
        Ticket ticket, 
        CancellationToken stoppingToken)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_gameServerUrl}/sessions", stoppingToken);
            if (!response.IsSuccessStatusCode) return null;

            var sessions = await response.Content.ReadFromJsonAsync<List<GameSession>>(cancellationToken: stoppingToken);
            if (sessions == null) return null;

            // Find an open session matching gameId, region, capacity, and latency threshold
            return sessions.FirstOrDefault(s =>
                s.GameId == gameId &&
                s.Region == region &&
                s.PlayerIds.Count < s.Capacity &&
                Math.Abs(ticket.TargetLatency - s.ReferenceLatency) <= latencyThreshold);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sweep: Error occurred while searching for open sessions.");
            return null;
        }
    }

    private async Task AssignToExistingSessionAsync(Ticket ticket, GameSession session, string region, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sweep: Backfilling player '{PlayerId}' into existing session '{SessionId}' in region '{Region}'...",
            ticket.PlayerId, session.Id, region);

        // 1. Add player to the session on the GameServer
        var backfillUrl = $"{_gameServerUrl}/sessions/{session.Id}/players?playerId={Uri.EscapeDataString(ticket.PlayerId)}";
        var response = await _httpClient.PostAsync(backfillUrl, content: null, stoppingToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Sweep: Failed to backfill player on GameServer. Status: {StatusCode}", response.StatusCode);
            return;
        }

        // 2. Mark ticket as matched in Storage
        var updateRequest = new UpdateStatusRequest
        {
            Status = TicketStatus.Matched,
            MatchedSessionId = session.Id,
            MatchedRegion = region
        };

        var updateResponse = await _httpClient.PutAsJsonAsync($"{_storageUrl}/tickets/{ticket.Id}/status", updateRequest, stoppingToken);
        if (!updateResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Sweep: Failed to update ticket status in storage for backfilled Ticket '{TicketId}'.", ticket.Id);
        }

        _logger.LogInformation("Sweep: Successfully backfilled player '{PlayerId}' into session '{SessionId}'.", ticket.PlayerId, session.Id);
    }
}
