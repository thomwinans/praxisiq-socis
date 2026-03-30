using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

/// <summary>
/// Data access contract for the snapp-users DynamoDB table.
/// Handles user profiles, PII (encrypted), and email-hash lookups.
/// </summary>
public interface IUserRepository
{
    /// <summary>Retrieves a user profile by ID. Returns null if not found.</summary>
    Task<User?> GetByIdAsync(string userId);

    /// <summary>Retrieves encrypted PII fields for a user. Returns null if not found.</summary>
    Task<UserPii?> GetPiiAsync(string userId);

    /// <summary>Looks up a userId by SHA-256 hashed email via GSI-Email. Returns null if not found.</summary>
    Task<string?> GetUserIdByEmailHashAsync(string emailHash);

    /// <summary>Creates a new user with profile, encrypted PII, and email-hash lookup item in a transaction.</summary>
    Task CreateAsync(User user, UserPii pii, string emailHash);

    /// <summary>Updates an existing user profile item.</summary>
    Task UpdateAsync(User user);

    /// <summary>Updates encrypted PII fields for a user.</summary>
    Task UpdatePiiAsync(UserPii pii);

    /// <summary>Searches users by specialty and geography via GSI-Specialty. Supports pagination.</summary>
    Task<List<User>> SearchBySpecialtyGeoAsync(string specialty, string geo, string? nextToken);
}
