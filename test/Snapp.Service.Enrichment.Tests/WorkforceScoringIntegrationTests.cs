using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;
using Snapp.Service.Enrichment.Services;
using Snapp.Service.Intelligence.Config;
using Snapp.Service.Intelligence.Handlers;
using Snapp.Shared.Constants;
using Snapp.Shared.Models;
using Xunit;

namespace Snapp.Service.Enrichment.Tests;

/// <summary>
/// End-to-end tests verifying that job posting fixtures flow through
/// enrichment, get stored as JOBPOST# signals, and feed into the
/// Intelligence scoring engine's Workforce dimension.
/// </summary>
public class WorkforceScoringIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly AmazonDynamoDBClient _client;
    private readonly EnrichmentRepository _enrichmentRepo;
    private readonly JobPostingLoader _loader;
    private readonly WorkforceEnrichmentProvider _workforceProvider;
    private readonly ScoringEngine _scoringEngine;

    public WorkforceScoringIntegrationTests()
    {
        _client = new AmazonDynamoDBClient(
            "fakeAccessKey", "fakeSecretKey",
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8042" });

        _enrichmentRepo = new EnrichmentRepository(
            _client,
            NullLogger<EnrichmentRepository>.Instance);

        var jobPostingSource = new FixtureJobPostingSource(
            NullLogger<FixtureJobPostingSource>.Instance);

        _loader = new JobPostingLoader(
            jobPostingSource,
            _enrichmentRepo,
            NullLogger<JobPostingLoader>.Instance);

        _workforceProvider = new WorkforceEnrichmentProvider(
            _client,
            NullLogger<WorkforceEnrichmentProvider>.Instance);

        // Build a minimal vertical config with the Workforce dimension
        var config = new VerticalConfig
        {
            Vertical = "dental",
            DisplayName = "Dental Practice",
            BaseConfidence = 40m,
            MaxConfidence = 95m,
            Dimensions =
            [
                new DimensionConfig
                {
                    Name = "FinancialHealth",
                    DisplayName = "Financial Health",
                    Weight = 0.22m,
                    Thresholds = new() { ["strong"] = 75, ["acceptable"] = 50, ["weak"] = 25 },
                    Kpis = [new KpiConfig { Name = "AnnualRevenue", DisplayName = "Annual Revenue", Unit = "USD", Category = "financial" }],
                },
                new DimensionConfig
                {
                    Name = "Workforce",
                    DisplayName = "Workforce Stability",
                    Weight = 0.10m,
                    Thresholds = new() { ["strong"] = 30, ["acceptable"] = 50, ["weak"] = 70 },
                    Kpis =
                    [
                        new KpiConfig { Name = "WorkforcePressureScore", DisplayName = "Workforce Pressure", Unit = "score", Category = "workforce_signals" },
                        new KpiConfig { Name = "PostingFrequency", DisplayName = "Posting Frequency", Unit = "ratio", Category = "workforce_signals" },
                        new KpiConfig { Name = "ChronicTurnoverSignal", DisplayName = "Chronic Turnover", Unit = "bool", Category = "workforce_signals" },
                        new KpiConfig { Name = "UrgentPostingRatio", DisplayName = "Urgent Ratio", Unit = "%", Category = "workforce_signals" },
                    ],
                },
            ],
            ContributionCategories =
            [
                new ContributionCategoryConfig { Category = "financial", Dimension = "FinancialHealth", ConfidenceWeight = 0.15m, DisplayName = "Financial Data" },
                new ContributionCategoryConfig { Category = "workforce_signals", Dimension = "Workforce", ConfidenceWeight = 0.08m, DisplayName = "Workforce Signals" },
            ],
        };

        _scoringEngine = new ScoringEngine(config);
    }

    public async Task InitializeAsync() => await _enrichmentRepo.EnsureTableAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task LoadAndAnalyzeAsync_CreatesJobPostSignalItems()
    {
        var analyses = await _loader.LoadAndAnalyzeAsync();

        // Verify JOBPOST# items were created
        var count = await _enrichmentRepo.CountSignalsByPrefixAsync("JOBPOST#");
        count.Should().BeGreaterOrEqualTo(15, "fixture contains 20 practices");
    }

    [Fact]
    public async Task LoadAndAnalyzeAsync_DetectsTurnoverSignals()
    {
        var analyses = await _loader.LoadAndAnalyzeAsync();

        // Practices with chronic turnover: Mitchell, Anderson, LA Dental Arts,
        // Naperville, Manhattan, Seattle Smiles (all have 3+ same-role postings)
        var chronicPractices = analyses.Where(a => a.ChronicTurnoverSignal).ToList();
        chronicPractices.Count.Should().BeGreaterOrEqualTo(4,
            "multiple practices have 3+ postings for the same role");

        // Verify role repetition detail
        foreach (var practice in chronicPractices)
        {
            practice.RoleRepetitions.Should().Contain(
                r => r.IsChronicTurnover && r.Count >= 3,
                $"{practice.PracticeName} should have at least one role with 3+ postings");
        }
    }

    [Fact]
    public async Task WorkforceProvider_ReturnsSignalsForPractice()
    {
        await _loader.LoadAndAnalyzeAsync();

        var signals = await _workforceProvider.GetWorkforceSignalsAsync(
            "test-user", "Mitchell Family Dentistry", "Phoenix", "AZ");

        signals.Should().HaveCount(1);
        var signal = signals[0];
        signal.Dimension.Should().Be("Workforce");
        signal.Category.Should().Be("workforce_signals");
        signal.DataPoints.Should().ContainKey("WorkforcePressureScore");
        signal.DataPoints.Should().ContainKey("PostingFrequency");
        signal.DataPoints.Should().ContainKey("ChronicTurnoverSignal");
    }

    [Fact]
    public async Task ScoringEngine_IncorporatesWorkforceSignals()
    {
        await _loader.LoadAndAnalyzeAsync();

        // Get workforce signals for a high-pressure practice
        var signals = await _workforceProvider.GetWorkforceSignalsAsync(
            "test-user", "Anderson Dental Group", "Houston", "TX");

        // Score with financial contributions only
        var financialContributions = new List<PracticeData>
        {
            new()
            {
                UserId = "test-user",
                Dimension = "FinancialHealth",
                Category = "financial",
                SubmittedAt = DateTime.UtcNow,
                DataPoints = new Dictionary<string, string>
                {
                    ["AnnualRevenue"] = "850000",
                },
            },
        };

        var resultWithout = _scoringEngine.ComputeScore(financialContributions);
        var resultWith = _scoringEngine.ComputeScore(financialContributions, signals);

        // With enrichment, Workforce dimension should have a score
        resultWith.DimensionScores.Should().ContainKey("Workforce");
        resultWith.DimensionScores["Workforce"].Should().BeGreaterThan(0,
            "workforce signals should produce a non-zero dimension score");

        // Without enrichment, Workforce should be 0
        resultWithout.DimensionScores.Should().ContainKey("Workforce");
        resultWithout.DimensionScores["Workforce"].Should().Be(0,
            "no workforce data means zero score");

        // Overall score should differ
        resultWith.OverallScore.Should().NotBe(resultWithout.OverallScore,
            "enrichment signals should affect the overall score");
    }

    [Fact]
    public async Task ScoringEngine_HighPressurePractice_LowerWorkforceScore()
    {
        await _loader.LoadAndAnalyzeAsync();

        // Anderson Dental Group: high pressure (chronic turnover + urgency)
        var highPressure = await _workforceProvider.GetWorkforceSignalsAsync(
            "test-user", "Anderson Dental Group", "Houston", "TX");

        // Dallas Premier Dental: low pressure (single non-urgent posting)
        var lowPressure = await _workforceProvider.GetWorkforceSignalsAsync(
            "test-user", "Dallas Premier Dental", "Dallas", "TX");

        var emptyContributions = new List<PracticeData>();

        var highPressureResult = _scoringEngine.ComputeScore(emptyContributions, highPressure);
        var lowPressureResult = _scoringEngine.ComputeScore(emptyContributions, lowPressure);

        // Higher pressure = lower stability score (inverse relationship)
        highPressureResult.DimensionScores["Workforce"].Should().BeLessThan(
            lowPressureResult.DimensionScores["Workforce"],
            "high-pressure practice should score lower on workforce stability");
    }

    [Fact]
    public async Task LoadAndAnalyzeAsync_StoresCorrectDynamoDBAttributes()
    {
        await _loader.LoadAndAnalyzeAsync();

        // Query Anderson Dental Group directly by PK
        var pk = "JOBPOST#Anderson Dental Group|Houston|TX";
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new(pk),
                [":prefix"] = new("ANALYSIS#"),
            },
        });

        response.Items.Should().NotBeEmpty();
        var item = response.Items[0];

        item["TotalPostings"].N.Should().Be("4");
        item["ChronicTurnoverSignal"].BOOL.Should().BeTrue();
        decimal.Parse(item["WorkforcePressureScore"].N).Should().BeGreaterThan(50m);
        item.Should().ContainKey("RoleRepetitions");
        item["GSI1PK"].S.Should().StartWith("JOBPOST#TX");
    }
}
