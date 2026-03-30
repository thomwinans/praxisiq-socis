using Snapp.Shared.Enums;

namespace Snapp.Shared.DTOs.Transaction;

public class DealRoomResponse
{
    public string DealId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;

    public DealStatus Status { get; set; }

    public int ParticipantCount { get; set; }

    public int DocumentCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
