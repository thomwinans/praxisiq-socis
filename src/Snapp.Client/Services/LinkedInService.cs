using System.Net.Http.Json;
using Snapp.Shared.DTOs.LinkedIn;

namespace Snapp.Client.Services;

public class LinkedInService : ILinkedInService
{
    private readonly HttpClient _http;

    public LinkedInService(HttpClient http)
    {
        _http = http;
    }

    public async Task<LinkedInAuthUrlResponse> GetAuthUrlAsync()
    {
        return await _http.GetFromJsonAsync<LinkedInAuthUrlResponse>("linkedin/auth-url")
               ?? new LinkedInAuthUrlResponse();
    }

    public async Task<LinkedInStatusResponse> GetStatusAsync()
    {
        return await _http.GetFromJsonAsync<LinkedInStatusResponse>("linkedin/status")
               ?? new LinkedInStatusResponse();
    }

    public async Task UnlinkAsync()
    {
        var response = await _http.PostAsync("linkedin/unlink", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<LinkedInShareResponse> ShareAsync(LinkedInShareRequest request)
    {
        var response = await _http.PostAsJsonAsync("linkedin/share", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LinkedInShareResponse>()
               ?? new LinkedInShareResponse();
    }
}
