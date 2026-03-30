using Snapp.Shared.Enums;

namespace Snapp.Shared.DTOs.Transaction;

public class ReferralResponse
{
    public string ReferralId { get; set; } = string.Empty;

    public string SenderUserId { get; set; } = string.Empty;

    public string ReceiverUserId { get; set; } = string.Empty;

    public string SenderDisplayName { get; set; } = string.Empty;

    public string ReceiverDisplayName { get; set; } = string.Empty;

    public string NetworkId { get; set; } = string.Empty;

    public string? Specialty { get; set; }

    public ReferralStatus Status { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? OutcomeRecordedAt { get; set; }
}
