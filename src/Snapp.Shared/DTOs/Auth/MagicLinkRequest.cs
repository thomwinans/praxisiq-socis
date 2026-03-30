using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Auth;

public class MagicLinkRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = string.Empty;
}
