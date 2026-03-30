using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Snapp.Client.State;

public class SnappAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private const string AccessTokenKey = "snapp_access_token";
    private const string RefreshTokenKey = "snapp_refresh_token";

    public SnappAuthStateProvider(IJSRuntime js)
    {
        _js = js;
    }

    public string? CurrentUserId { get; private set; }
    public bool IsAuthenticated => CurrentUserId is not null;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await GetAccessTokenAsync();

        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var claims = ParseClaimsFromJwt(token);
        if (claims is null)
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        CurrentUserId = claims.FindFirst("sub")?.Value;
        return new AuthenticationState(claims);
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await _js.InvokeAsync<string?>("authInterop.getToken", AccessTokenKey);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await _js.InvokeAsync<string?>("authInterop.getToken", RefreshTokenKey);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        await _js.InvokeVoidAsync("authInterop.setToken", AccessTokenKey, accessToken);
        await _js.InvokeVoidAsync("authInterop.setToken", RefreshTokenKey, refreshToken);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task ClearTokensAsync()
    {
        await _js.InvokeVoidAsync("authInterop.removeToken", AccessTokenKey);
        await _js.InvokeVoidAsync("authInterop.removeToken", RefreshTokenKey);
        CurrentUserId = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static ClaimsPrincipal? ParseClaimsFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
                return null;

            var payload = parts[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes);
            if (keyValuePairs is null)
                return null;

            // Check expiration
            if (keyValuePairs.TryGetValue("exp", out var exp))
            {
                var expTime = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
                if (expTime < DateTimeOffset.UtcNow)
                    return null;
            }

            var claims = new List<Claim>();
            foreach (var kvp in keyValuePairs)
            {
                if (kvp.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in kvp.Value.EnumerateArray())
                        claims.Add(new Claim(kvp.Key, element.ToString()));
                }
                else
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value.ToString()));
                }
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64.Replace('-', '+').Replace('_', '/'));
    }
}
