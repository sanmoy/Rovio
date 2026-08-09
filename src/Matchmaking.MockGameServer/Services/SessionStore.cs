using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Matchmaking.Shared.Models;

namespace Matchmaking.MockGameServer.Services;

public class SessionStore
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();

    public GameSession CreateSession(SessionRequest request)
    {
        var sessionId = $"session-{Guid.NewGuid():N}";
        var session = new GameSession
        {
            Id = sessionId,
            GameId = request.GameId,
            Region = request.Region,
            Capacity = request.Capacity,
            PlayerIds = request.PlayerIds,
            ReferenceLatency = request.ReferenceLatency
        };
        _sessions[sessionId] = session;
        return session;
    }

    public bool AddPlayerToSession(string sessionId, string playerId)
    {
        while (_sessions.TryGetValue(sessionId, out var session))
        {
            if (session.PlayerIds.Count >= session.Capacity)
            {
                return false;
            }

            var updatedPlayers = new List<string>(session.PlayerIds) { playerId };
            var updatedSession = session with { PlayerIds = updatedPlayers };
            if (_sessions.TryUpdate(sessionId, updatedSession, session))
            {
                return true;
            }
        }
        return false;
    }

    public GameSession? GetSession(string id)
    {
        _sessions.TryGetValue(id, out var session);
        return session;
    }

    public IEnumerable<GameSession> GetAllSessions()
    {
        return _sessions.Values;
    }
}
