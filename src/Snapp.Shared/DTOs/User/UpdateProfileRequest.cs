using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.User;

public class UpdateProfileRequest
{
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    public string? Specialty { get; set; }

    public string? Geography { get; set; }
}
