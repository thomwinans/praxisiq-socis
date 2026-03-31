namespace Snapp.Service.LinkedIn.Clients;

/// <summary>
/// Abstracts LinkedIn API calls. Real implementation hits LinkedIn REST APIs;
/// mock implementation returns synthetic data for local dev/testing.
/// </summary>
public interface ILinkedInClient
{
    /// <summary>
    /// Exchanges an OAuth authorization code for an access token.
    /// Returns (accessToken, expiresInSeconds).
    /// </summary>
    Task<(string AccessToken, int ExpiresIn)> ExchangeCodeForTokenAsync(string code, string redirectUri);

    /// <summary>
    /// Fetches the authenticated user's profile from LinkedIn userinfo endpoint.
    /// Returns (sub/URN, name, headline, photoUrl).
    /// </summary>
    Task<LinkedInProfile> GetProfileAsync(string accessToken);

    /// <summary>
    /// Publishes a text post to LinkedIn via the Share API (w_member_social scope).
    /// Returns the LinkedIn post URL.
    /// </summary>
    Task<string> SharePostAsync(string accessToken, string linkedInUrn, string content);
}

public class LinkedInProfile
{
    public string Sub { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Headline { get; set; }
    public string? PhotoUrl { get; set; }
}
