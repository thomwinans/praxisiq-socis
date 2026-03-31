using System.ComponentModel.DataAnnotations;

namespace Snapp.Service.Transaction.Models;

public class Attestation
{
    [Required]
    public string TargetUserId { get; set; } = string.Empty;

    [Required]
    public string AttestorUserId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Skill { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }
}
