using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.User;

public class OnboardingRequest
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Specialty { get; set; }

    [MaxLength(200)]
    public string? Geography { get; set; }

    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [Url, MaxLength(500)]
    public string? LinkedInProfileUrl { get; set; }
}
