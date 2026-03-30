namespace Snapp.Shared.Models;

/// <summary>
/// Represents a magic link token stored in snapp-users table.
/// PK: TOKEN#{Code}, SK: MAGIC_LINK
/// Single-use, 15-minute TTL.
/// </summary>
public class MagicLinkToken
{
    /// <summary>URL-safe code (64 chars) used as the PK suffix.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the email that requested the link.</summary>
    public string HashedEmail { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>TTL attribute — DynamoDB auto-deletes after this time.</summary>
    public DateTime ExpiresAt { get; set; }
}
