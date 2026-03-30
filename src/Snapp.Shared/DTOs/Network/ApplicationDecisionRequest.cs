using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Network;

public class ApplicationDecisionRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, RegularExpression("^(Approved|Denied)$", ErrorMessage = "Decision must be 'Approved' or 'Denied'.")]
    public string Decision { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Reason { get; set; }
}
