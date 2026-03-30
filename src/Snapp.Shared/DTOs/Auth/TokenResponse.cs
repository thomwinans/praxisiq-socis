namespace Snapp.Shared.DTOs.Auth;

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public int ExpiresIn { get; set; }

    /// <summary>True if this is the user's first login (auto-created account). Used to trigger onboarding flow.</summary>
    public bool IsNewUser { get; set; }
}
