using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

/// <summary>
/// Represents a participant in a deal room.
/// PK: DEAL#{DealId}, SK: PART#{UserId}
/// </summary>
public class DealParticipant
{
    [Required]
    public string DealId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Participant's role in the deal: seller, buyer, or advisor.</summary>
    [Required]
    public string Role { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; }
}
