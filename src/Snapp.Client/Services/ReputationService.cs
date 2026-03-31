using System.Net.Http.Json;
using Snapp.Shared.DTOs.Transaction;

namespace Snapp.Client.Services;

public class ReputationService : IReputationService
{
    private readonly HttpClient _http;

    public ReputationService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ReputationResponse?> GetReputationAsync(string userId)
    {
        return await _http.GetFromJsonAsync<ReputationResponse>($"tx/reputation/{userId}");
    }

    public async Task<ReputationHistoryResponse> GetHistoryAsync(string userId)
    {
        return await _http.GetFromJsonAsync<ReputationHistoryResponse>($"tx/reputation/{userId}/history")
               ?? new ReputationHistoryResponse();
    }

    public async Task<AttestationListResponse> GetAttestationsAsync(string userId)
    {
        return await _http.GetFromJsonAsync<AttestationListResponse>($"tx/reputation/{userId}/attestations")
               ?? new AttestationListResponse();
    }

    public async Task<bool> RequestAttestationAsync(string userId)
    {
        var response = await _http.PostAsync($"tx/reputation/{userId}/attestations/request", null);
        return response.IsSuccessStatusCode;
    }
}
