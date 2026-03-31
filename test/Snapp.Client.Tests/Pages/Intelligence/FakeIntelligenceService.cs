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
    public BenchmarkResponse? BenchmarkResult { get; set; }
    public ValuationResponse? ValuationResult { get; set; }
    public ValuationResponse? ScenarioResult { get; set; }
    public CareerStageResponse? CareerStageResult { get; set; }
    public MarketProfileResponse? MarketProfileResult { get; set; }
    public MarketCompareResponse? MarketCompareResult { get; set; }
    public PendingQuestionsResponse? PendingQuestionsResult { get; set; }
    public AnswerQuestionResponse? AnswerQuestionResult { get; set; }
    public ProgressionResponse? ProgressionResult { get; set; }
    public CompensationBenchmarkResponse? CompensationBenchmarkResult { get; set; }
    public bool ThrowOnDashboard { get; set; }
    public bool ThrowOnContribute { get; set; }
    public bool ThrowOnBenchmarks { get; set; }
    public bool ThrowOnValuation { get; set; }
    public bool ThrowOnMarket { get; set; }
    public string? LastAnsweredQuestionId { get; set; }
    public string? LastAnswer { get; set; }

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

    public Task<BenchmarkResponse?> GetBenchmarksAsync(string specialty, string geography, string sizeBand)
    {
        if (ThrowOnBenchmarks) throw new HttpRequestException("Failed");
        return Task.FromResult(BenchmarkResult);
    }

    public Task<ValuationResponse?> GetValuationAsync()
    {
        if (ThrowOnValuation) throw new HttpRequestException("Failed");
        return Task.FromResult(ValuationResult);
    }

    public Task<ValuationResponse?> ComputeScenarioAsync(Dictionary<string, string> overrides)
        => Task.FromResult(ScenarioResult);

    public Task<CareerStageResponse?> GetCareerStageAsync()
        => Task.FromResult(CareerStageResult);

    public Task<MarketProfileResponse?> GetMarketProfileAsync(string geoId)
    {
        if (ThrowOnMarket) throw new HttpRequestException("Failed");
        return Task.FromResult(MarketProfileResult);
    }

    public Task<MarketCompareResponse?> CompareMarketsAsync(string[] geoIds)
        => Task.FromResult(MarketCompareResult);

    public Task<PendingQuestionsResponse?> GetPendingQuestionsAsync()
        => Task.FromResult(PendingQuestionsResult);

    public Task<AnswerQuestionResponse?> AnswerQuestionAsync(string questionId, string answer)
    {
        LastAnsweredQuestionId = questionId;
        LastAnswer = answer;
        return Task.FromResult(AnswerQuestionResult);
    }

    public Task<ProgressionResponse?> GetProgressionAsync()
        => Task.FromResult(ProgressionResult);

    public Task<CompensationBenchmarkResponse?> GetCompensationBenchmarksAsync(string? market, string? size)
        => Task.FromResult(CompensationBenchmarkResult);
}
