using Microsoft.Kiota.Abstractions;
using Snapp.Client.State;
using Snapp.Sdk;
using SdkModels = Snapp.Sdk.Models;
using SharedUser = Snapp.Shared.DTOs.User;

namespace Snapp.Client.Services;

/// <summary>
/// Thin wrapper around the Kiota-generated SnappApiClient.
/// Handles token refresh on 401 and maps Kiota exceptions to user-friendly errors.
/// </summary>
public class SnappSdkWrapper
{
    private readonly SnappApiClient _client;
    private readonly SnappAuthStateProvider _authState;
    private readonly IAuthService _authService;

    public SnappSdkWrapper(
        SnappApiClient client,
        SnappAuthStateProvider authState,
        IAuthService authService)
    {
        _client = client;
        _authState = authState;
        _authService = authService;
    }

    /// <summary>
    /// The underlying Kiota client, exposed for advanced/direct usage.
    /// </summary>
    public SnappApiClient Client => _client;

    public virtual async Task<SharedUser.ProfileResponse> GetMyProfileAsync()
    {
        var sdkProfile = await ExecuteWithRetryAsync(() => _client.Api.Users.Me.GetAsync());
        return MapProfile(sdkProfile);
    }

    public virtual async Task<SharedUser.ProfileResponse> GetProfileAsync(string userId)
    {
        var sdkProfile = await ExecuteWithRetryAsync(() => _client.Api.Users[userId].GetAsync());
        return MapProfile(sdkProfile);
    }

    /// <summary>
    /// Executes an SDK call, retrying once on 401 after refreshing the token.
    /// Maps Kiota ApiException to SnappApiException for consistent error handling.
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T?>> call) where T : class
    {
        try
        {
            return await call() ?? throw new SnappApiException("Received empty response from API.");
        }
        catch (SdkModels.ErrorResponse ex) when (ex.ResponseStatusCode == 401)
        {
            // Attempt token refresh
            var refreshToken = await _authState.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new SnappApiException("Session expired. Please sign in again.", isAuthError: true);

            try
            {
                var tokenResponse = await _authService.RefreshAsync(refreshToken);
                await _authState.SetTokensAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken);
                return await call() ?? throw new SnappApiException("Received empty response from API.");
            }
            catch
            {
                await _authState.ClearTokensAsync();
                throw new SnappApiException("Session expired. Please sign in again.", isAuthError: true);
            }
        }
        catch (SdkModels.ErrorResponse ex)
        {
            throw new SnappApiException(
                $"API error (HTTP {ex.ResponseStatusCode}): {ex.Message}");
        }
        catch (ApiException ex)
        {
            throw new SnappApiException(
                $"API error (HTTP {ex.ResponseStatusCode}): {ex.Message}");
        }
    }

    private static SharedUser.ProfileResponse MapProfile(SdkModels.ProfileResponse? sdk)
    {
        if (sdk is null) return new SharedUser.ProfileResponse();

        return new SharedUser.ProfileResponse
        {
            UserId = sdk.UserId ?? string.Empty,
            DisplayName = sdk.DisplayName ?? string.Empty,
            Specialty = sdk.Specialty,
            Geography = sdk.Geography,
            LinkedInProfileUrl = sdk.LinkedInProfileUrl,
            PhotoUrl = sdk.PhotoUrl,
            HasPracticeData = sdk.HasPracticeData ?? false,
            ProfileCompleteness = (decimal)(sdk.ProfileCompleteness ?? 0),
            CreatedAt = sdk.CreatedAt?.DateTime ?? default
        };
    }
}

/// <summary>
/// User-friendly exception type for SDK errors.
/// </summary>
public class SnappApiException : Exception
{
    public bool IsAuthError { get; }

    public SnappApiException(string message, bool isAuthError = false) : base(message)
    {
        IsAuthError = isAuthError;
    }
}
