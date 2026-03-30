namespace Snapp.Shared.Models;

/// <summary>
/// Represents a refresh token / session stored in snapp-users table.
/// PK: REFRESH#{TokenHash}, SK: SESSION
/// 30-day TTL. Token is SHA-256 hashed before storage.
/// </summary>
public class RefreshToken
{
    /// <summary>SHA-256 hash of the actual refresh token string.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>The user this session belongs to.</summary>
    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>TTL attribute — DynamoDB auto-deletes after this time.</summary>
    public DateTime ExpiresAt { get; set; }
}
