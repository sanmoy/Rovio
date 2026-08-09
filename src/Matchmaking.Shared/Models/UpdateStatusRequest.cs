namespace Matchmaking.Shared.Models;

public record UpdateStatusRequest
{
    public TicketStatus Status { get; init; }
    public string? MatchedSessionId { get; init; }
    public string? MatchedRegion { get; init; }
}
