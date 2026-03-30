namespace Snapp.Shared.DTOs.LinkedIn;

public class LinkedInAuthUrlResponse
{
    /// <summary>LinkedIn OAuth 2.0 authorization URL to redirect the user to.</summary>
    public string AuthorizationUrl { get; set; } = string.Empty;
}
