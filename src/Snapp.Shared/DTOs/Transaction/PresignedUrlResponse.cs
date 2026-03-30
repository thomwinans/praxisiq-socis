namespace Snapp.Shared.DTOs.Transaction;

/// <summary>
/// Response containing a pre-signed S3 URL for direct upload or download of deal documents.
/// </summary>
public class PresignedUrlResponse
{
    public string Url { get; set; } = string.Empty;

    /// <summary>URL expiry in seconds.</summary>
    public int ExpiresIn { get; set; }
}
