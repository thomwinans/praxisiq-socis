using System.ComponentModel.DataAnnotations;

namespace Snapp.Service.Transaction.DTOs;

public class CreateAttestationRequest
{
    [Required]
    public string TargetUserId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Skill { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Comment { get; set; }
}
