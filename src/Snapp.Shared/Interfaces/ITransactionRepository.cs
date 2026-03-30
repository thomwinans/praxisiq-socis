using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

/// <summary>
/// Data access contract for the snapp-tx DynamoDB table.
/// Handles referrals, reputation scores, and related transaction records.
/// </summary>
public interface ITransactionRepository
{
    /// <summary>Creates a new referral with sender and receiver index items in a transaction.</summary>
    Task CreateReferralAsync(Referral referral);

    /// <summary>Updates a referral's status or outcome fields.</summary>
    Task UpdateReferralAsync(Referral referral);

    /// <summary>Retrieves a referral by ID. Returns null if not found.</summary>
    Task<Referral?> GetReferralAsync(string referralId);

    /// <summary>Lists referrals sent by a user, ordered by timestamp descending. Supports pagination.</summary>
    Task<List<Referral>> ListSentReferralsAsync(string userId, string? nextToken);

    /// <summary>Lists referrals received by a user, ordered by timestamp descending. Supports pagination.</summary>
    Task<List<Referral>> ListReceivedReferralsAsync(string userId, string? nextToken);

    /// <summary>Retrieves the current computed reputation for a user. Returns null if not yet computed.</summary>
    Task<Reputation?> GetReputationAsync(string userId);

    /// <summary>Saves a reputation score snapshot and updates the current reputation pointer.</summary>
    Task SaveReputationAsync(Reputation reputation);
}
