using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

/// <summary>
/// Represents LinkedIn OAuth data stored in snapp-users table.
/// PK: USER#{UserId}, SK: LINKEDIN
/// All token values are encrypted via IFieldEncryptor.
/// </summary>
public class LinkedInToken
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Encrypted LinkedIn URN (person identifier).</summary>
    [Required]
    public string EncryptedLinkedInURN { get; set; } = string.Empty;

    /// <summary>Encrypted OAuth access token.</summary>
    [Required]
    public string EncryptedAccessToken { get; set; } = string.Empty;

    /// <summary>When the access token expires.</summary>
    public DateTime TokenExpiry { get; set; }

    /// <summary>Encryption key ID used, for key rotation support.</summary>
    [Required]
    public string EncryptionKeyId { get; set; } = string.Empty;
}
