using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Transaction;

public class CreateReferralRequest
{
    [Required]
    public string ReceiverUserId { get; set; } = string.Empty;

    [Required]
    public string NetworkId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Specialty { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
