namespace Snapp.Shared.DTOs.Transaction;

public class AttestationResponse
{
    public string AttestationId { get; set; } = string.Empty;

    public string FromUserId { get; set; } = string.Empty;

    public string FromDisplayName { get; set; } = string.Empty;

    public string ToUserId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class AttestationListResponse
{
    public List<AttestationResponse> Attestations { get; set; } = new();
}
