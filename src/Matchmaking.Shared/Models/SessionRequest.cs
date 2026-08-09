using System.Collections.Generic;

namespace Matchmaking.Shared.Models;

public record SessionRequest
{
    public string GameId { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public List<string> PlayerIds { get; init; } = [];
    public int ReferenceLatency { get; init; }
}
