using System.ComponentModel.DataAnnotations;
using Snapp.Shared.Enums;

namespace Snapp.Shared.Models;

public class Referral
{
    [Required]
    public string ReferralId { get; set; } = string.Empty;

    [Required]
    public string SenderUserId { get; set; } = string.Empty;

    [Required]
    public string ReceiverUserId { get; set; } = string.Empty;

    [Required]
    public string NetworkId { get; set; } = string.Empty;

    public string? Specialty { get; set; }

    public ReferralStatus Status { get; set; } = ReferralStatus.Created;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? OutcomeRecordedAt { get; set; }
}
