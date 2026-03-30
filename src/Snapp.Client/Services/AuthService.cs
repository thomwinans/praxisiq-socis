using System.Net.Http.Json;
using Snapp.Shared.DTOs.Auth;
using Snapp.Shared.DTOs.Common;

namespace Snapp.Client.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _http;

    public AuthService(HttpClient http)
    {
        _http = http;
    }

    public async Task<MessageResponse> RequestMagicLinkAsync(string email)
    {
        var response = await _http.PostAsJsonAsync("auth/magic-link", new MagicLinkRequest { Email = email });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MessageResponse>() ?? new MessageResponse();
    }

    public async Task<TokenResponse> ValidateCodeAsync(string code)
    {
        var response = await _http.PostAsJsonAsync("auth/validate", new MagicLinkValidateRequest { Code = code });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResponse>() ?? new TokenResponse();
    }

    public async Task<TokenResponse> RefreshAsync(string refreshToken)
    {
        var response = await _http.PostAsJsonAsync("auth/refresh", new RefreshRequest { RefreshToken = refreshToken });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResponse>() ?? new TokenResponse();
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _http.PostAsync("auth/logout", null);
        }
        catch
        {
            // Best-effort — clear local tokens regardless
        }
    }
}
