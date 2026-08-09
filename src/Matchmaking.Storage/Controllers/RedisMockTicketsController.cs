using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Matchmaking.Shared.Models;
using Matchmaking.Storage.Services;

namespace Matchmaking.Storage.Controllers;

/// <summary>
/// REST Controller acting as a mock Redis database layer for storing and querying matchmaking tickets.
/// In production, this layer is replaced directly by Redis or an equivalent high-throughput key-value store.
/// </summary>
[ApiController]
[Route("tickets")]
public class RedisMockTicketsController : ControllerBase
{
    private readonly TicketStore _store;

    public RedisMockTicketsController(TicketStore store)
    {
        _store = store;
    }

    [HttpPost]
    public ActionResult<Ticket> Create([FromBody] Ticket ticket)
    {
        _store.Add(ticket);
        return Ok(ticket);
    }

    [HttpGet("{id}")]
    public ActionResult<Ticket> Get(string id)
    {
        var ticket = _store.Get(id);
        if (ticket == null)
        {
            return NotFound();
        }
        return Ok(ticket);
    }

    [HttpPut("{id}/status")]
    public IActionResult UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        var success = _store.UpdateStatus(id, request.Status, request.MatchedSessionId, request.MatchedRegion);
        if (!success)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpGet]
    public ActionResult<IEnumerable<Ticket>> Query(
        [FromQuery] string? gameId,
        [FromQuery] string? region,
        [FromQuery] TicketStatus? status)
    {
        var query = _store.GetAll();

        if (!string.IsNullOrEmpty(gameId))
        {
            query = query.Where(t => t.GameId == gameId);
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (!string.IsNullOrEmpty(region))
        {
            query = query.Where(t => t.TargetRegion == region || t.RegionalLatencies.ContainsKey(region));
        }

        return Ok(query.ToList());
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        var success = _store.Remove(id);
        if (!success)
        {
            return NotFound();
        }
        return NoContent();
    }
}
