namespace Snapp.Shared.DTOs.Transaction;

public class DealParticipantResponse
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; }
}
