using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

/// <summary>
/// Represents an audit trail entry for a deal room.
/// PK: DEAL#{DealId}, SK: AUDIT#{Timestamp}#{EventId}
/// Every action in a deal room is logged.
/// </summary>
public class DealAuditEntry
{
    [Required]
    public string EventId { get; set; } = string.Empty;

    [Required]
    public string DealId { get; set; } = string.Empty;

    /// <summary>Action performed (e.g., "participant_added", "document_uploaded", "deal_created").</summary>
    [Required]
    public string Action { get; set; } = string.Empty;

    /// <summary>User who performed the action.</summary>
    [Required]
    public string ActorUserId { get; set; } = string.Empty;

    /// <summary>Optional action-specific details (e.g., filename, participant role).</summary>
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; }
}
