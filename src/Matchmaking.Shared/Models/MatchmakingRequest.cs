using System.ComponentModel.DataAnnotations;

namespace Matchmaking.Shared.Models;

public record MatchmakingRequest
{
    [Required]
    public string PlayerId { get; init; } = string.Empty;

    [Required]
    public Dictionary<string, int> RegionalLatencies { get; init; } = [];
}
