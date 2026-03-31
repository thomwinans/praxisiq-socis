namespace Snapp.Service.Transaction.DTOs;

public class AttestationResponse
{
    public string TargetUserId { get; set; } = string.Empty;

    public string AttestorUserId { get; set; } = string.Empty;

    public string Skill { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }
}
