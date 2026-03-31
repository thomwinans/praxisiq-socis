using Snapp.Client.Services;
using Snapp.Shared.DTOs.Transaction;

namespace Snapp.Client.Tests.Mocks;

public class MockReferralService : IReferralService
{
    public ReferralListResponse SentReferrals { get; set; } = new();
    public ReferralListResponse ReceivedReferrals { get; set; } = new();
    public ReferralResponse? CreatedReferral { get; set; }
    public ReferralResponse? UpdatedReferral { get; set; }
    public ReferralResponse? OutcomeReferral { get; set; }
    public bool ShouldThrow { get; set; }

    public CreateReferralRequest? LastCreateRequest { get; private set; }
    public string? LastUpdateReferralId { get; private set; }
    public string? LastOutcomeReferralId { get; private set; }

    public Task<ReferralListResponse> GetSentAsync()
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(SentReferrals);
    }

    public Task<ReferralListResponse> GetReceivedAsync()
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(ReceivedReferrals);
    }

    public Task<ReferralResponse?> CreateAsync(CreateReferralRequest request)
    {
        LastCreateRequest = request;
        return Task.FromResult(CreatedReferral);
    }

    public Task<ReferralResponse?> UpdateStatusAsync(string referralId, UpdateReferralStatusRequest request)
    {
        LastUpdateReferralId = referralId;
        return Task.FromResult(UpdatedReferral);
    }

    public Task<ReferralResponse?> RecordOutcomeAsync(string referralId, RecordOutcomeRequest request)
    {
        LastOutcomeReferralId = referralId;
        return Task.FromResult(OutcomeReferral);
    }
}
