using System.Net.Http.Json;
using Snapp.Shared.DTOs.User;

namespace Snapp.Client.Services;

public class UserService : IUserService
{
    private readonly HttpClient _http;

    public UserService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ProfileResponse> GetMyProfileAsync()
    {
        var response = await _http.GetAsync("users/me");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProfileResponse>() ?? new ProfileResponse();
    }

    public async Task<ProfileResponse> GetProfileAsync(string userId)
    {
        var response = await _http.GetAsync($"users/{userId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProfileResponse>() ?? new ProfileResponse();
    }

    public async Task<ProfileResponse> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var response = await _http.PutAsJsonAsync("users/me", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProfileResponse>() ?? new ProfileResponse();
    }

    public async Task<ProfileResponse> OnboardAsync(OnboardingRequest request)
    {
        var response = await _http.PostAsJsonAsync("users/me/onboard", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProfileResponse>() ?? new ProfileResponse();
    }

    public async Task<PiiResponse> GetMyPiiAsync()
    {
        var response = await _http.GetAsync("users/me/pii");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PiiResponse>() ?? new PiiResponse();
    }
}
