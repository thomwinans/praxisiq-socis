using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Network;

public class ApplicationDecisionRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Decision { get; set; } = string.Empty;

    public string? Reason { get; set; }
}
