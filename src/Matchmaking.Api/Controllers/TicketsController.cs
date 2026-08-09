using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Matchmaking.Shared.Models;
using Matchmaking.Api.Clients;

namespace Matchmaking.Api.Controllers;

[ApiController]
[Route("games/{gameId}/matchmaking/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly StorageClient _storageClient;

    public TicketsController(StorageClient storageClient)
    {
        _storageClient = storageClient;
    }

    [HttpPost]
    public async Task<ActionResult<Ticket>> Create(string gameId, [FromBody] MatchmakingRequest request)
    {
        string targetRegion = string.Empty;
        int targetLatency = 0;
        if (request.RegionalLatencies != null && request.RegionalLatencies.Count > 0)
        {
            var minLatency = request.RegionalLatencies.MinBy(kvp => kvp.Value);
            targetRegion = minLatency.Key;
            targetLatency = minLatency.Value;
        }

        var ticket = new Ticket
        {
            Id = Guid.NewGuid().ToString(),
            PlayerId = request.PlayerId,
            GameId = gameId,
            Status = TicketStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            RegionalLatencies = request.RegionalLatencies ?? [],
            TargetRegion = targetRegion,
            TargetLatency = targetLatency
        };

        var created = await _storageClient.CreateTicketAsync(ticket);
        if (created == null)
        {
            return StatusCode(500, "Failed to save the matchmaking ticket to storage.");
        }

        return Ok(created);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Ticket>> Get(string gameId, string id)
    {
        var ticket = await _storageClient.GetTicketAsync(id);
        if (ticket == null || ticket.GameId != gameId)
        {
            return NotFound($"Ticket with ID '{id}' not found for game '{gameId}'.");
        }

        return Ok(ticket);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(string gameId, string id)
    {
        var ticket = await _storageClient.GetTicketAsync(id);
        if (ticket == null || ticket.GameId != gameId)
        {
            return NotFound($"Ticket with ID '{id}' not found for game '{gameId}'.");
        }

        var success = await _storageClient.CancelTicketAsync(id);
        if (!success)
        {
            return StatusCode(500, "Failed to cancel the matchmaking ticket.");
        }

        return NoContent();
    }
}
