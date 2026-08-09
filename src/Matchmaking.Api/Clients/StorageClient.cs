using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Matchmaking.Shared.Models;

namespace Matchmaking.Api.Clients;

public class StorageClient
{
    private readonly HttpClient _httpClient;

    public StorageClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var storageUrl = configuration["StorageService:Url"] ?? "http://localhost:5010";
        _httpClient.BaseAddress = new Uri(storageUrl);
    }

    public async Task<Ticket?> CreateTicketAsync(Ticket ticket)
    {
        var response = await _httpClient.PostAsJsonAsync("/tickets", ticket);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Ticket>();
        }
        return null;
    }

    public async Task<Ticket?> GetTicketAsync(string id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/tickets/{id}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Ticket>();
            }
        }
        catch
        {
            // Fail silently or handle connection issues, returning null
        }
        return null;
    }

    public async Task<bool> CancelTicketAsync(string id)
    {
        // Update status in storage to Cancelled so the client poll gets a Cancelled status,
        // rather than a 404 (NotFound).
        var response = await _httpClient.PutAsJsonAsync($"/tickets/{id}/status", new UpdateStatusRequest
        {
            Status = TicketStatus.Cancelled
        });
        return response.IsSuccessStatusCode;
    }
}
