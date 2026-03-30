using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

public interface ITransactionRepository
{
    Task CreateReferralAsync(Referral referral);

    Task UpdateReferralAsync(Referral referral);

    Task<Referral?> GetReferralAsync(string referralId);

    Task<List<Referral>> ListSentReferralsAsync(string userId, string? nextToken);

    Task<List<Referral>> ListReceivedReferralsAsync(string userId, string? nextToken);

    Task<Reputation?> GetReputationAsync(string userId);

    Task SaveReputationAsync(Reputation reputation);
}
