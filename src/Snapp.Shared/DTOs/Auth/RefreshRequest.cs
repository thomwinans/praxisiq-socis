using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Auth;

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
