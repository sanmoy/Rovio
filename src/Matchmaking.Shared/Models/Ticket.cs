using System;
using System.Collections.Generic;

namespace Matchmaking.Shared.Models;

public record Ticket
{
    public string Id { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
    public string GameId { get; init; } = string.Empty;
    public TicketStatus Status { get; init; } = TicketStatus.Queued;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, int> RegionalLatencies { get; init; } = [];
    public string TargetRegion { get; init; } = string.Empty;
    public int TargetLatency { get; init; }
    public string? MatchedSessionId { get; init; }
    public string? MatchedRegion { get; init; }
}
