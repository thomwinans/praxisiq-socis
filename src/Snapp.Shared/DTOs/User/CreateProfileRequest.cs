using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.User;

public class CreateProfileRequest
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public string? Specialty { get; set; }

    public string? Geography { get; set; }
}
