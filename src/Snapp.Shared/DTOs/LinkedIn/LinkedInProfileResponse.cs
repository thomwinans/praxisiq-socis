namespace Snapp.Shared.DTOs.LinkedIn;

/// <summary>
/// Returned after successful LinkedIn OAuth callback.
/// Contains profile data pulled from LinkedIn for pre-populating SNAPP profile.
/// </summary>
public class LinkedInProfileResponse
{
    public string LinkedInName { get; set; } = string.Empty;

    public string? LinkedInHeadline { get; set; }

    public string? PhotoUrl { get; set; }
}
