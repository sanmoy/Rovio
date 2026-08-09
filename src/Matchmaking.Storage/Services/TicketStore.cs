using System.Collections.Concurrent;
using System.Collections.Generic;
using Matchmaking.Shared.Models;

namespace Matchmaking.Storage.Services;

public class TicketStore
{
    private readonly ConcurrentDictionary<string, Ticket> _tickets = new();

    public void Add(Ticket ticket)
    {
        _tickets[ticket.Id] = ticket;
    }

    public Ticket? Get(string id)
    {
        _tickets.TryGetValue(id, out var ticket);
        return ticket;
    }

    public bool UpdateStatus(string id, TicketStatus status, string? matchedSessionId, string? matchedRegion)
    {
        if (!_tickets.TryGetValue(id, out var existing))
        {
            return false;
        }

        var updated = existing with
        {
            Status = status,
            MatchedSessionId = matchedSessionId,
            MatchedRegion = matchedRegion
        };

        return _tickets.TryUpdate(id, updated, existing);
    }

    public IEnumerable<Ticket> GetAll()
    {
        return _tickets.Values;
    }

    public bool Remove(string id)
    {
        return _tickets.TryRemove(id, out _);
    }
}
