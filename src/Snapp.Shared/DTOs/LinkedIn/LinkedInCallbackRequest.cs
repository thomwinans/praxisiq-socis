using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.LinkedIn;

public class LinkedInCallbackRequest
{
    /// <summary>OAuth authorization code from LinkedIn redirect.</summary>
    [Required]
    public string Code { get; set; } = string.Empty;

    /// <summary>CSRF state token for validation.</summary>
    [Required]
    public string State { get; set; } = string.Empty;
}
