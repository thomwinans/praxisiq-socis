namespace Snapp.Shared.DTOs.Transaction;

public class DealDocumentResponse
{
    public string DocumentId { get; set; } = string.Empty;

    public string Filename { get; set; } = string.Empty;

    public string UploadedByUserId { get; set; } = string.Empty;

    public string UploadedByDisplayName { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTime CreatedAt { get; set; }
}
