using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Auth;

public class MagicLinkRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
