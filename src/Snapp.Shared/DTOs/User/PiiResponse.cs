namespace Snapp.Shared.DTOs.User;

/// <summary>
/// Decrypted PII fields returned only to the owning user via GET /api/users/me/pii.
/// </summary>
public class PiiResponse
{
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? ContactInfo { get; set; }
}
