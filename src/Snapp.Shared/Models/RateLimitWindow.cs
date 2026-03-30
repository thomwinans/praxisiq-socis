namespace Snapp.Shared.Models;

/// <summary>
/// Represents a rate limit window stored in snapp-users table.
/// PK: RATE#{HashedEmail}#MAGIC, SK: WINDOW#{yyyyMMddHHmm}
/// 15-minute TTL. Used to limit magic link requests per email.
/// </summary>
public class RateLimitWindow
{
    /// <summary>SHA-256 hash of the email being rate-limited.</summary>
    public string HashedEmail { get; set; } = string.Empty;

    /// <summary>Window key in yyyyMMddHHmm format.</summary>
    public string WindowKey { get; set; } = string.Empty;

    /// <summary>Number of requests in this window.</summary>
    public int Count { get; set; }

    /// <summary>TTL attribute — DynamoDB auto-deletes after this time.</summary>
    public DateTime ExpiresAt { get; set; }
}
