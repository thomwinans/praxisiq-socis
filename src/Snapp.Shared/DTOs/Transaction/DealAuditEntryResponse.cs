namespace Snapp.Shared.DTOs.Transaction;

public class DealAuditEntryResponse
{
    public string EventId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorDisplayName { get; set; } = string.Empty;

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; }
}
