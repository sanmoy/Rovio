using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;
using Matchmaking.Shared.Models;
using Matchmaking.Worker;

namespace Matchmaking.Tests;

public class QueueTests
{
    [Fact]
    public void Ticket_Creation_SetsCorrectProperties()
    {
        // Arrange & Act
        var latencies = new Dictionary<string, int> { { "eu-west-1", 80 }, { "us-east-1", 25 }, { "ap-south-1", 110 } };
        var minLatency = latencies.MinBy(kvp => kvp.Value);

        var ticket = new Ticket
        {
            Id = "test-id",
            PlayerId = "player-1",
            GameId = "game-1",
            Status = TicketStatus.Queued,
            RegionalLatencies = latencies,
            TargetRegion = minLatency.Key,
            TargetLatency = minLatency.Value
        };

        // Assert
        Assert.Equal("test-id", ticket.Id);
        Assert.Equal("player-1", ticket.PlayerId);
        Assert.Equal("game-1", ticket.GameId);
        Assert.Equal(TicketStatus.Queued, ticket.Status);
        Assert.Equal(80, ticket.RegionalLatencies["eu-west-1"]);
        Assert.Equal("us-east-1", ticket.TargetRegion);
        Assert.Equal(25, ticket.TargetLatency);
    }

    [Fact]
    public void Partition_Discovery_DerivesFromTargetRegion()
    {
        // Arrange
        var tickets = new List<Ticket>
        {
            new Ticket { GameId = "game1", TargetRegion = "us-east", TargetLatency = 20 },
            new Ticket { GameId = "game1", TargetRegion = "us-east", TargetLatency = 35 },
            new Ticket { GameId = "game1", TargetRegion = "eu-west", TargetLatency = 40 },
            new Ticket { GameId = "game2", TargetRegion = "us-east", TargetLatency = 15 }
        };

        // Act
        var partitions = tickets.Select(t => (t.GameId, t.TargetRegion)).Distinct().ToList();

        // Assert
        Assert.Equal(3, partitions.Count);
        Assert.Contains(("game1", "us-east"), partitions);
        Assert.Contains(("game1", "eu-west"), partitions);
        Assert.Contains(("game2", "us-east"), partitions);
    }

    [Theory]
    [InlineData(0, 30)]    // 0s waited -> 30ms threshold
    [InlineData(4, 30)]    // 4s waited -> 30ms threshold (grow interval is 5s)
    [InlineData(5, 40)]    // 5s waited -> 40ms threshold (grow rate is 10ms)
    [InlineData(9, 40)]    // 9s waited -> 40ms threshold
    [InlineData(10, 50)]   // 10s waited -> 50ms threshold
    [InlineData(15, 60)]   // 15s waited -> 60ms threshold
    [InlineData(85, 200)]  // 85s waited -> 200ms threshold (cap reached)
    [InlineData(100, 200)] // 100s waited -> capped at 200ms threshold
    public void LatencyThreshold_ShouldGrowBasedOnWaitTime(int waitSeconds, int expectedThreshold)
    {
        // Arrange
        var logger = NullLogger<PartitionMatcherService>.Instance;
        var factory = new StubHttpClientFactory();
        var config = new StubConfiguration();
        var service = new PartitionMatcherService(logger, factory, config);

        // Act
        var threshold = service.GetLatencyThreshold(TimeSpan.FromSeconds(waitSeconds));

        // Assert
        Assert.Equal(expectedThreshold, threshold);
    }

    [Fact]
    public void LatencyThreshold_ShouldBeMonotonicAndCapped()
    {
        // Arrange
        var logger = NullLogger<PartitionMatcherService>.Instance;
        var factory = new StubHttpClientFactory();
        var config = new StubConfiguration();
        var service = new PartitionMatcherService(logger, factory, config);

        int previousThreshold = 0;

        // Act & Assert
        for (int seconds = 0; seconds <= 150; seconds++)
        {
            var threshold = service.GetLatencyThreshold(TimeSpan.FromSeconds(seconds));
            
            // Should never decrease
            Assert.True(threshold >= previousThreshold, $"Threshold decreased at {seconds} seconds.");
            
            // Should always be between 30ms and 200ms
            Assert.True(threshold >= 30 && threshold <= 200, $"Threshold {threshold} out of bounds [30, 200] at {seconds} seconds.");

            previousThreshold = threshold;
        }

        // Capped threshold value should be reached and maintained
        Assert.Equal(200, previousThreshold);
    }
}

#region Stubs for Testing

public class StubHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new HttpClient();
}

public class StubConfiguration : IConfiguration
{
    private readonly Dictionary<string, string> _values = new();

    public string? this[string key]
    {
        get => _values.TryGetValue(key, out var val) ? val : null;
        set => _values[key] = value!;
    }

    public IConfigurationSection GetSection(string key) => new StubConfigurationSection(this, key);

    public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();

    public IChangeToken GetReloadToken() => NullChangeToken.Singleton;

    private class NullChangeToken : IChangeToken
    {
        public static NullChangeToken Singleton { get; } = new();
        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => false;
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new NullDisposable();
        private class NullDisposable : IDisposable { public void Dispose() {} }
    }
}

public class StubConfigurationSection : IConfigurationSection
{
    private readonly StubConfiguration _config;

    public StubConfigurationSection(StubConfiguration config, string key)
    {
        _config = config;
        Key = key;
        Path = key;
    }

    public string? this[string key]
    {
        get => _config[$"{Path}:{key}"];
        set => _config[$"{Path}:{key}"] = value!;
    }

    public string Key { get; }
    public string Path { get; }
    public string? Value
    {
        get => _config[Path];
        set => _config[Path] = value!;
    }

    public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
    public IChangeToken GetReloadToken() => throw new NotImplementedException();
    public IConfigurationSection GetSection(string key) => new StubConfigurationSection(_config, $"{Path}:{key}");
}

#endregion
