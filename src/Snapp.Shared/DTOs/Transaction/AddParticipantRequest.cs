using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Transaction;

public class AddParticipantRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Role in the deal: "seller", "buyer", or "advisor".</summary>
    [Required]
    public string Role { get; set; } = string.Empty;
}
