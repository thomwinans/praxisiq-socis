using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

/// <summary>
/// Data access contract for LinkedIn integration data in the snapp-users DynamoDB table.
/// Handles encrypted LinkedIn OAuth tokens and URN storage.
/// PK: USER#{UserId}, SK: LINKEDIN
/// </summary>
public interface ILinkedInRepository
{
    /// <summary>
    /// Stores or updates LinkedIn OAuth data for a user.
    /// All token values must be encrypted via IFieldEncryptor before calling this method.
    /// </summary>
    Task SaveAsync(LinkedInToken token);

    /// <summary>
    /// Retrieves LinkedIn OAuth data for a user. Returns null if not linked.
    /// Returned values are encrypted — caller must decrypt via IFieldEncryptor.
    /// </summary>
    Task<LinkedInToken?> GetAsync(string userId);

    /// <summary>
    /// Deletes LinkedIn OAuth data for a user (unlink).
    /// Idempotent — does not fail if not linked.
    /// </summary>
    Task DeleteAsync(string userId);
}
