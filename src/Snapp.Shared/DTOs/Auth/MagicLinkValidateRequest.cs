using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Auth;

public class MagicLinkValidateRequest
{
    [Required, MinLength(32)]
    public string Code { get; set; } = string.Empty;
}
