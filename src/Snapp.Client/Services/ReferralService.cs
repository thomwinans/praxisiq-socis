using System.Net.Http.Json;
using Snapp.Shared.DTOs.Transaction;

namespace Snapp.Client.Services;

public class ReferralService : IReferralService
{
    private readonly HttpClient _http;

    public ReferralService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ReferralListResponse> GetSentAsync()
    {
        return await _http.GetFromJsonAsync<ReferralListResponse>("tx/referrals/sent")
               ?? new ReferralListResponse();
    }

    public async Task<ReferralListResponse> GetReceivedAsync()
    {
        return await _http.GetFromJsonAsync<ReferralListResponse>("tx/referrals/received")
               ?? new ReferralListResponse();
    }

    public async Task<ReferralResponse?> CreateAsync(CreateReferralRequest request)
    {
        var response = await _http.PostAsJsonAsync("tx/referrals", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<ReferralResponse>();
        return null;
    }

    public async Task<ReferralResponse?> UpdateStatusAsync(string referralId, UpdateReferralStatusRequest request)
    {
        var response = await _http.PutAsJsonAsync($"tx/referrals/{referralId}/status", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<ReferralResponse>();
        return null;
    }

    public async Task<ReferralResponse?> RecordOutcomeAsync(string referralId, RecordOutcomeRequest request)
    {
        var response = await _http.PostAsJsonAsync($"tx/referrals/{referralId}/outcome", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<ReferralResponse>();
        return null;
    }
}
