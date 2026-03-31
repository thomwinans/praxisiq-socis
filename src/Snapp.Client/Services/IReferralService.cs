using Snapp.Shared.DTOs.Transaction;

namespace Snapp.Client.Services;

public interface IReferralService
{
    Task<ReferralListResponse> GetSentAsync();
    Task<ReferralListResponse> GetReceivedAsync();
    Task<ReferralResponse?> CreateAsync(CreateReferralRequest request);
    Task<ReferralResponse?> UpdateStatusAsync(string referralId, UpdateReferralStatusRequest request);
    Task<ReferralResponse?> RecordOutcomeAsync(string referralId, RecordOutcomeRequest request);
}
