using Snapp.Shared.DTOs.Transaction;

namespace Snapp.Client.Services;

public interface IReputationService
{
    Task<ReputationResponse?> GetReputationAsync(string userId);
    Task<ReputationHistoryResponse> GetHistoryAsync(string userId);
    Task<AttestationListResponse> GetAttestationsAsync(string userId);
    Task<bool> RequestAttestationAsync(string userId);
}
