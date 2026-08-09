using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Matchmaking.Shared.Models;
using Matchmaking.MockGameServer.Services;

namespace Matchmaking.MockGameServer.Controllers;

/// <summary>
/// Controller for the Mock Game Server simulating game session creation, capacity management, and player registration.
/// NOTE: Matchmaking.MockGameServer is a mock implementation for simulation/testing purposes, not part of the production core service.
/// </summary>
[ApiController]
[Route("[controller]")]
public class SessionsController : ControllerBase
{
    private readonly SessionStore _store;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(SessionStore store, ILogger<SessionsController> logger)
    {
        _store = store;
        _logger = logger;
    }

    [HttpPost]
    public ActionResult<GameSession> Create([FromBody] SessionRequest request)
    {
        var session = _store.CreateSession(request);
        _logger.LogInformation("Successfully spawned game session '{SessionId}' for game '{GameId}' in region '{Region}' with {PlayerCount}/{Capacity} players.",
            session.Id, session.GameId, session.Region, session.PlayerIds.Count, session.Capacity);
        return Ok(session);
    }

    [HttpGet("{id}")]
    public ActionResult<GameSession> Get(string id)
    {
        var session = _store.GetSession(id);
        if (session == null)
        {
            return NotFound();
        }
        return Ok(session);
    }

    [HttpGet]
    public ActionResult<IEnumerable<GameSession>> GetAll()
    {
        return Ok(_store.GetAllSessions());
    }

    [HttpPost("{id}/players")]
    public IActionResult AddPlayer(string id, [FromQuery] string playerId)
    {
        var success = _store.AddPlayerToSession(id, playerId);
        if (!success)
        {
            return BadRequest($"Failed to add player '{playerId}' to session '{id}'. The session might be full or non-existent.");
        }
        _logger.LogInformation("Successfully backfilled player '{PlayerId}' into session '{SessionId}'.", playerId, id);
        return NoContent();
    }
}
