using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

public class DealDocument
{
    [Required]
    public string DocumentId { get; set; } = string.Empty;

    [Required]
    public string DealId { get; set; } = string.Empty;

    [Required]
    public string Filename { get; set; } = string.Empty;

    [Required]
    public string S3Key { get; set; } = string.Empty;

    [Required]
    public string UploadedByUserId { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTime CreatedAt { get; set; }
}
