using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.User;

public class OnboardingRequest
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public string? Specialty { get; set; }

    public string? Geography { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? LinkedInProfileUrl { get; set; }
}
