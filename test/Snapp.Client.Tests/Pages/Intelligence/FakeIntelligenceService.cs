using Snapp.Client.Services;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Intelligence;

namespace Snapp.Client.Tests.Pages.Intelligence;

public class FakeIntelligenceService : IIntelligenceService
{
    public DashboardResponse? DashboardResult { get; set; }
    public ScoreResponse? ScoreResult { get; set; }
    public ContributionListResponse? ContributionsResult { get; set; }
    public MessageResponse? ContributeResult { get; set; }
    public VerticalConfigResponse? ConfigResult { get; set; }
    public bool ThrowOnDashboard { get; set; }
    public bool ThrowOnContribute { get; set; }

    public Task<DashboardResponse?> GetDashboardAsync()
    {
        if (ThrowOnDashboard) throw new HttpRequestException("Failed");
        return Task.FromResult(DashboardResult);
    }

    public Task<ScoreResponse?> GetScoreAsync()
        => Task.FromResult(ScoreResult);

    public Task<ContributionListResponse?> GetContributionsAsync()
        => Task.FromResult(ContributionsResult);

    public Task<MessageResponse?> ContributeDataAsync(SubmitDataRequest request)
    {
        if (ThrowOnContribute) throw new HttpRequestException("Failed");
        return Task.FromResult(ContributeResult);
    }

    public Task<VerticalConfigResponse?> GetVerticalConfigAsync()
        => Task.FromResult(ConfigResult);
}
