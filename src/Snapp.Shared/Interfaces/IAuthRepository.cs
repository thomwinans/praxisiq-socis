using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

/// <summary>
/// Data access contract for authentication entities in the snapp-users DynamoDB table.
/// Handles magic link tokens, refresh tokens/sessions, and rate limiting.
/// </summary>
public interface IAuthRepository
{
    /// <summary>
    /// Stores a magic link token. PK=TOKEN#{code}, SK=MAGIC_LINK.
    /// Token is single-use with a 15-minute TTL.
    /// </summary>
    Task CreateMagicLinkAsync(MagicLinkToken token);

    /// <summary>
    /// Retrieves a magic link token by code. Returns null if not found or expired.
    /// Does NOT consume the token — caller must call <see cref="DeleteMagicLinkAsync"/> after use.
    /// </summary>
    Task<MagicLinkToken?> GetMagicLinkAsync(string code);

    /// <summary>
    /// Deletes a magic link token (single-use consumption).
    /// Idempotent — does not fail if already deleted or expired.
    /// </summary>
    Task DeleteMagicLinkAsync(string code);

    /// <summary>
    /// Stores a refresh token / session. PK=REFRESH#{tokenHash}, SK=SESSION.
    /// Token hash is SHA-256 of the actual token string. 30-day TTL.
    /// </summary>
    Task CreateRefreshTokenAsync(RefreshToken token);

    /// <summary>
    /// Retrieves a refresh token by its SHA-256 hash. Returns null if not found or expired.
    /// </summary>
    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash);

    /// <summary>
    /// Deletes a refresh token (used during logout and token rotation).
    /// Idempotent — does not fail if already deleted or expired.
    /// </summary>
    Task DeleteRefreshTokenAsync(string tokenHash);

    /// <summary>
    /// Increments the rate limit counter for a magic link request.
    /// Uses conditional write: attribute_not_exists OR Count &lt; limit.
    /// Returns true if the request is within the rate limit, false if exceeded.
    /// PK=RATE#{hashedEmail}#MAGIC, SK=WINDOW#{windowKey}.
    /// </summary>
    Task<bool> TryIncrementRateLimitAsync(string hashedEmail, string windowKey, int maxRequests);
}
