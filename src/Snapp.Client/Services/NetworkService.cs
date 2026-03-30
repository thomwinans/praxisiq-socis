using System.Net.Http.Json;
using Snapp.Shared.DTOs.Network;

namespace Snapp.Client.Services;

public class NetworkService : INetworkService
{
    private readonly HttpClient _http;

    public NetworkService(HttpClient http)
    {
        _http = http;
    }

    public async Task<NetworkListResponse> GetMyNetworksAsync()
    {
        return await _http.GetFromJsonAsync<NetworkListResponse>("networks/mine")
               ?? new NetworkListResponse();
    }

    public async Task<NetworkListResponse> GetAllNetworksAsync()
    {
        return await _http.GetFromJsonAsync<NetworkListResponse>("networks")
               ?? new NetworkListResponse();
    }

    public async Task<NetworkResponse?> GetNetworkAsync(string networkId)
    {
        try
        {
            return await _http.GetFromJsonAsync<NetworkResponse>($"networks/{networkId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<NetworkResponse?> CreateNetworkAsync(CreateNetworkRequest request)
    {
        var response = await _http.PostAsJsonAsync("networks", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<NetworkResponse>();
        return null;
    }

    public async Task<bool> UpdateNetworkAsync(string networkId, UpdateNetworkRequest request)
    {
        var response = await _http.PutAsJsonAsync($"networks/{networkId}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<MemberListResponse> GetMembersAsync(string networkId)
    {
        return await _http.GetFromJsonAsync<MemberListResponse>($"networks/{networkId}/members")
               ?? new MemberListResponse();
    }

    public async Task<bool> ApplyAsync(string networkId, string? applicationText)
    {
        var request = new ApplyRequest { NetworkId = networkId, ApplicationText = applicationText };
        var response = await _http.PostAsJsonAsync($"networks/{networkId}/apply", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ApplicationResponse>> GetApplicationsAsync(string networkId)
    {
        return await _http.GetFromJsonAsync<List<ApplicationResponse>>($"networks/{networkId}/applications")
               ?? new List<ApplicationResponse>();
    }

    public async Task<bool> DecideApplicationAsync(string networkId, string userId, string decision, string? reason = null)
    {
        var request = new ApplicationDecisionRequest { UserId = userId, Decision = decision, Reason = reason };
        var response = await _http.PostAsJsonAsync($"networks/{networkId}/applications/{userId}/decide", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<NetworkSettingsResponse?> GetSettingsAsync(string networkId)
    {
        try
        {
            return await _http.GetFromJsonAsync<NetworkSettingsResponse>($"networks/{networkId}/settings");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<bool> RemoveMemberAsync(string networkId, string userId)
    {
        var response = await _http.DeleteAsync($"networks/{networkId}/members/{userId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ChangeMemberRoleAsync(string networkId, string userId, string newRole)
    {
        var response = await _http.PutAsJsonAsync($"networks/{networkId}/members/{userId}/role",
            new { Role = newRole });
        return response.IsSuccessStatusCode;
    }
}
