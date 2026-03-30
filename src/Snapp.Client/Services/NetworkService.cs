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
}
