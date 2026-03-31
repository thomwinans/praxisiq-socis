using Snapp.Client.Services;
using Snapp.Shared.DTOs.Transaction;

namespace Snapp.Client.Tests.Mocks;

public class MockReputationService : IReputationService
{
    public ReputationResponse? Reputation { get; set; }
    public ReputationHistoryResponse History { get; set; } = new();
    public AttestationListResponse Attestations { get; set; } = new();
    public bool AttestationRequestResult { get; set; } = true;
    public bool ShouldThrow { get; set; }

    public string? LastRequestedUserId { get; private set; }

    public Task<ReputationResponse?> GetReputationAsync(string userId)
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        LastRequestedUserId = userId;
        return Task.FromResult(Reputation);
    }

    public Task<ReputationHistoryResponse> GetHistoryAsync(string userId)
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(History);
    }

    public Task<AttestationListResponse> GetAttestationsAsync(string userId)
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(Attestations);
    }

    public Task<bool> RequestAttestationAsync(string userId)
    {
        LastRequestedUserId = userId;
        return Task.FromResult(AttestationRequestResult);
    }
}
