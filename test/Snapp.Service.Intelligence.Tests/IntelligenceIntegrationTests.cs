using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Snapp.Service.Intelligence.Endpoints;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Intelligence;
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Intelligence.Tests;

[Collection(DockerTestCollection.Name)]
public class IntelligenceIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly HttpClient _http;
    private readonly DynamoDbTestHelper _dynamo;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public IntelligenceIntegrationTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
        _http = new HttpClient { BaseAddress = new Uri(fixture.KongUrl), Timeout = TimeSpan.FromSeconds(30) };
        _dynamo = new DynamoDbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _dynamo.EnsureUsersTableAsync();
        await EnsureIntelTableAsync();
    }

    public Task DisposeAsync()
    {
        _http.Dispose();
        _dynamo.Dispose();
        return Task.CompletedTask;
    }

    // ── Data Contribution Tests ─────────────────────────────────

    [Fact]
    public async Task Contribute_ValidCategory_ReturnsOk()
    {
        var jwt = await AuthenticateAsync($"intel-contrib-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string>
            {
                ["AnnualRevenue"] = "850000",
                ["OverheadRatio"] = "62",
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Message.Should().Contain("Confidence score");
    }

    [Fact]
    public async Task Contribute_InvalidCategory_ReturnsBadRequest()
    {
        var jwt = await AuthenticateAsync($"intel-badcat-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "nonexistent_category",
            DataPoints = new Dictionary<string, string> { ["foo"] = "bar" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Contribute_ConfidenceIncreases_WithMoreData()
    {
        var jwt = await AuthenticateAsync($"intel-conf-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // First contribution
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string> { ["AnnualRevenue"] = "900000" },
        });

        // Get dashboard — check initial confidence
        var dash1 = await _http.GetFromJsonAsync<DashboardResponse>("/api/intel/dashboard", JsonOptions);
        var conf1 = dash1!.ConfidenceScore;

        // Second contribution — different category
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "operations",
            DataPoints = new Dictionary<string, string> { ["ChairUtilization"] = "78" },
        });

        // Dashboard should show higher confidence
        var dash2 = await _http.GetFromJsonAsync<DashboardResponse>("/api/intel/dashboard", JsonOptions);
        dash2!.ConfidenceScore.Should().BeGreaterThan(conf1);
    }

    [Fact]
    public async Task ListContributions_ReturnsContributedCategories()
    {
        var jwt = await AuthenticateAsync($"intel-list-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string> { ["AnnualRevenue"] = "1000000" },
        });

        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "client_base",
            DataPoints = new Dictionary<string, string> { ["ActivePatientCount"] = "2500" },
        });

        var response = await _http.GetAsync("/api/intel/contributions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ContributionListResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Categories.Count.Should().BeGreaterOrEqualTo(2);
        body.TotalConfidence.Should().BeGreaterThan(40m);
    }

    // ── Scoring Tests ───────────────────────────────────────────

    [Fact]
    public async Task ComputeScore_WithData_ReturnsScoreProfile()
    {
        var jwt = await AuthenticateAsync($"intel-score-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Contribute data first
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string>
            {
                ["AnnualRevenue"] = "950000",
                ["OverheadRatio"] = "60",
                ["CollectionsRate"] = "95",
            },
        });

        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "operations",
            DataPoints = new Dictionary<string, string>
            {
                ["ChairUtilization"] = "82",
                ["NewPatientsPerMonth"] = "35",
            },
        });

        // Compute score
        var scoreResp = await _http.PostAsync("/api/intel/score/compute", null);
        scoreResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var score = await scoreResp.Content.ReadFromJsonAsync<ScoreResponse>(JsonOptions);
        score.Should().NotBeNull();
        score!.OverallScore.Should().BeGreaterThan(0);
        score.DimensionScores.Should().ContainKey("FinancialHealth");
        score.DimensionScores.Should().ContainKey("Operations");
        score.ConfidenceLevel.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetScore_AfterCompute_ReturnsSameScore()
    {
        var jwt = await AuthenticateAsync($"intel-getscore-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string> { ["AnnualRevenue"] = "750000" },
        });

        var computeResp = await _http.PostAsync("/api/intel/score/compute", null);
        var computed = await computeResp.Content.ReadFromJsonAsync<ScoreResponse>(JsonOptions);

        var getResp = await _http.GetAsync("/api/intel/score");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await getResp.Content.ReadFromJsonAsync<ScoreResponse>(JsonOptions);
        fetched!.OverallScore.Should().Be(computed!.OverallScore);
    }

    [Fact]
    public async Task GetScore_NoCompute_ReturnsNotFound()
    {
        var jwt = await AuthenticateAsync($"intel-noscore-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/score");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ScoreHistory_AfterMultipleComputes_ReturnsTrend()
    {
        var jwt = await AuthenticateAsync($"intel-hist-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // First round of data
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string> { ["AnnualRevenue"] = "500000" },
        });
        await _http.PostAsync("/api/intel/score/compute", null);

        // Add more data
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "operations",
            DataPoints = new Dictionary<string, string> { ["ChairUtilization"] = "85" },
        });
        await _http.PostAsync("/api/intel/score/compute", null);

        var histResp = await _http.GetAsync("/api/intel/score/history");
        histResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await histResp.Content.ReadFromJsonAsync<ScoreHistoryResponse>(JsonOptions);
        history!.History.Count.Should().BeGreaterOrEqualTo(2);
    }

    // ── Dashboard Tests ─────────────────────────────────────────

    [Fact]
    public async Task Dashboard_WithContributions_ReturnsKPIs()
    {
        var jwt = await AuthenticateAsync($"intel-dash-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string>
            {
                ["AnnualRevenue"] = "1200000",
                ["ProfitMargin"] = "22",
            },
        });

        var response = await _http.GetAsync("/api/intel/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions);
        dashboard.Should().NotBeNull();
        dashboard!.KPIs.Count.Should().BeGreaterOrEqualTo(2);
        dashboard.ConfidenceScore.Should().BeGreaterThan(40m);
    }

    [Fact]
    public async Task Dashboard_NoAuth_ReturnsUnauthorized()
    {
        _http.DefaultRequestHeaders.Authorization = null;

        var response = await _http.GetAsync("/api/intel/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Benchmark Tests ─────────────────────────────────────────

    [Fact]
    public async Task Benchmarks_WithSeededData_ReturnsCohort()
    {
        var jwt = await AuthenticateAsync($"intel-bench-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Seed benchmark data directly
        await SeedBenchmarkDataAsync("general-dentistry", "national", "small");

        var response = await _http.GetAsync("/api/intel/benchmarks?specialty=general-dentistry&geo=national&size=small");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BenchmarkResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Metrics.Count.Should().BeGreaterOrEqualTo(1);
        body.Metrics.First().P25.Should().BeGreaterThan(0);
        body.Metrics.First().P50.Should().BeGreaterThan(0);
        body.Metrics.First().P75.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Benchmarks_MissingParams_ReturnsBadRequest()
    {
        var jwt = await AuthenticateAsync($"intel-benchbad-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/benchmarks?specialty=general-dentistry");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Career Stage Tests ───────────────────────────────────────

    [Fact]
    public async Task ComputeCareerStage_MaturePractitioner_ReturnsMatureStage()
    {
        var jwt = await AuthenticateAsync($"intel-stage-mature-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/career-stage/compute", new
        {
            TenureYears = 15,
            CoLocationCount = 0,
            ProductionVolume = 1200000,
            EntityType = "group",
            ProviderCount = 3,
            LocationCount = 1,
            OwnerProductionPct = 45,
            HasSuccessionPlan = true,
            HasEntityFormation = true,
            CeHoursRecent = 30,
            ReputationScore = 80,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CareerStageResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Stage.Should().Be("Mature");
        body.ConfidenceLevel.Should().NotBeNullOrEmpty();
        body.TriggerSignals.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ComputeCareerStage_PreExitSolo_ReturnsPreExitWithRisks()
    {
        var jwt = await AuthenticateAsync($"intel-stage-preexit-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/career-stage/compute", new
        {
            TenureYears = 25,
            CoLocationCount = 0,
            ProductionVolume = 600000,
            EntityType = "solo",
            ProviderCount = 1,
            LocationCount = 1,
            OwnerProductionPct = 92,
            HasSuccessionPlan = false,
            HasEntityFormation = true,
            CeHoursRecent = 5,
            ReputationScore = 70,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CareerStageResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Stage.Should().Be("PreExit");
        body.RiskFlags.Should().NotBeEmpty();
        body.RiskFlags.Should().Contain(r => r.Type == "retirement_risk");
        body.RiskFlags.Should().Contain(r => r.Type == "succession_risk");
        body.RiskFlags.Should().Contain(r => r.Type == "key_person_dependency");
    }

    [Fact]
    public async Task ComputeCareerStage_GrowthPractice_ReturnsGrowthStage()
    {
        var jwt = await AuthenticateAsync($"intel-stage-growth-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/career-stage/compute", new
        {
            TenureYears = 5,
            CoLocationCount = 0,
            ProductionVolume = 900000,
            EntityType = "group",
            ProviderCount = 4,
            LocationCount = 2,
            OwnerProductionPct = 35,
            HasSuccessionPlan = false,
            HasEntityFormation = true,
            CeHoursRecent = 40,
            ReputationScore = 75,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CareerStageResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Stage.Should().Be("Growth");
    }

    [Fact]
    public async Task ComputeCareerStage_TrainingEntry_ReturnsTrainingStage()
    {
        var jwt = await AuthenticateAsync($"intel-stage-entry-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/career-stage/compute", new
        {
            TenureYears = 1,
            CoLocationCount = 0,
            ProductionVolume = 0,
            EntityType = "",
            ProviderCount = 0,
            LocationCount = 0,
            OwnerProductionPct = 0,
            HasSuccessionPlan = false,
            HasEntityFormation = false,
            CeHoursRecent = 50,
            ReputationScore = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CareerStageResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Stage.Should().Be("TrainingEntry");
    }

    [Fact]
    public async Task ComputeCareerStage_Associate_ReturnsAssociateStage()
    {
        var jwt = await AuthenticateAsync($"intel-stage-assoc-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/career-stage/compute", new
        {
            TenureYears = 3,
            CoLocationCount = 2,
            ProductionVolume = 200000,
            EntityType = "group",
            ProviderCount = 1,
            LocationCount = 1,
            OwnerProductionPct = 30,
            HasSuccessionPlan = false,
            HasEntityFormation = false,
            CeHoursRecent = 35,
            ReputationScore = 50,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CareerStageResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Stage.Should().Be("Associate");
    }

    [Fact]
    public async Task GetCareerStage_AfterCompute_ReturnsSameStage()
    {
        var jwt = await AuthenticateAsync($"intel-stage-get-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await _http.PostAsJsonAsync("/api/intel/career-stage/compute", new
        {
            TenureYears = 12,
            CoLocationCount = 0,
            ProductionVolume = 1000000,
            EntityType = "group",
            ProviderCount = 2,
            LocationCount = 1,
            OwnerProductionPct = 50,
            HasSuccessionPlan = true,
            HasEntityFormation = true,
            CeHoursRecent = 25,
            ReputationScore = 85,
        });

        var getResp = await _http.GetAsync("/api/intel/career-stage");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var stage = await getResp.Content.ReadFromJsonAsync<CareerStageResponse>(JsonOptions);
        stage.Should().NotBeNull();
        stage!.Stage.Should().Be("Mature");
    }

    [Fact]
    public async Task GetCareerStage_NoCompute_ReturnsNotFound()
    {
        var jwt = await AuthenticateAsync($"intel-stage-nf-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/career-stage");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CareerStageHistory_AfterMultipleComputes_ReturnsTransitions()
    {
        var jwt = await AuthenticateAsync($"intel-stage-hist-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // First compute — entry stage
        await _http.PostAsJsonAsync("/api/intel/career-stage/compute", new
        {
            TenureYears = 1,
            CoLocationCount = 0,
            ProductionVolume = 0,
            EntityType = "",
            ProviderCount = 0,
            LocationCount = 0,
            OwnerProductionPct = 0,
            HasSuccessionPlan = false,
            HasEntityFormation = false,
            CeHoursRecent = 40,
            ReputationScore = 0,
        });

        // Second compute — growth stage
        await _http.PostAsJsonAsync("/api/intel/career-stage/compute", new
        {
            TenureYears = 6,
            CoLocationCount = 0,
            ProductionVolume = 800000,
            EntityType = "group",
            ProviderCount = 3,
            LocationCount = 2,
            OwnerProductionPct = 40,
            HasSuccessionPlan = false,
            HasEntityFormation = true,
            CeHoursRecent = 30,
            ReputationScore = 70,
        });

        var histResp = await _http.GetAsync("/api/intel/career-stage/history");
        histResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await histResp.Content.ReadFromJsonAsync<CareerStageHistoryResponse>(JsonOptions);
        history!.History.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task ComputeCareerStage_NoAuth_ReturnsUnauthorized()
    {
        _http.DefaultRequestHeaders.Authorization = null;

        var response = await _http.PostAsJsonAsync("/api/intel/career-stage/compute", new
        {
            TenureYears = 5,
            ProviderCount = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ComputeCareerStage_OverextendedGrowth_ReturnsOverextensionRisk()
    {
        var jwt = await AuthenticateAsync($"intel-stage-overext-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/career-stage/compute", new
        {
            TenureYears = 7,
            CoLocationCount = 0,
            ProductionVolume = 2000000,
            EntityType = "group",
            ProviderCount = 8,
            LocationCount = 4,
            OwnerProductionPct = 20,
            HasSuccessionPlan = true,
            HasEntityFormation = true,
            CeHoursRecent = 40,
            ReputationScore = 85,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CareerStageResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Stage.Should().Be("Growth");
        body.RiskFlags.Should().Contain(r => r.Type == "overextension");
    }

    // ── Valuation Tests ───────────────────────────────────────────

    [Fact]
    public async Task ComputeValuation_WithContributions_ReturnsThreeCases()
    {
        var jwt = await AuthenticateAsync($"intel-val-compute-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Seed benchmarks
        await SeedBenchmarkDataAsync("general-dentistry", "national", "small");

        // Contribute financial data
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string>
            {
                ["AnnualRevenue"] = "900000",
                ["ProfitMargin"] = "22",
            },
        });

        // Contribute owner risk data
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "owner_risk",
            DataPoints = new Dictionary<string, string>
            {
                ["OwnerProductionPct"] = "75",
                ["ProviderCount"] = "2",
                ["SuccessionPlanExists"] = "false",
            },
        });

        var response = await _http.PostAsync("/api/intel/valuation/compute", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ValuationResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Downside.Should().BeGreaterThan(0);
        body.Base.Should().BeGreaterThan(body.Downside);
        body.Upside.Should().BeGreaterThan(body.Base);
        body.ConfidenceScore.Should().BeGreaterThan(40);
        body.Drivers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetValuation_AfterCompute_ReturnsSameWithHistory()
    {
        var jwt = await AuthenticateAsync($"intel-val-get-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await SeedBenchmarkDataAsync("general-dentistry", "national", "small");

        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string> { ["AnnualRevenue"] = "1000000", ["ProfitMargin"] = "25" },
        });

        var computeResp = await _http.PostAsync("/api/intel/valuation/compute", null);
        var computed = await computeResp.Content.ReadFromJsonAsync<ValuationResponse>(JsonOptions);

        var getResp = await _http.GetAsync("/api/intel/valuation");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await getResp.Content.ReadFromJsonAsync<ValuationResponse>(JsonOptions);
        fetched!.Base.Should().Be(computed!.Base);
        fetched.History.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetValuation_NoCompute_ReturnsNotFound()
    {
        var jwt = await AuthenticateAsync($"intel-val-nf-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/valuation");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ScenarioValuation_WithOverrides_ProducesDifferentResults()
    {
        var jwt = await AuthenticateAsync($"intel-val-scenario-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await SeedBenchmarkDataAsync("general-dentistry", "national", "small");

        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string> { ["AnnualRevenue"] = "900000", ["ProfitMargin"] = "20" },
        });

        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "owner_risk",
            DataPoints = new Dictionary<string, string> { ["OwnerProductionPct"] = "80" },
        });

        // Compute baseline
        var baseResp = await _http.PostAsync("/api/intel/valuation/compute", null);
        var baseline = await baseResp.Content.ReadFromJsonAsync<ValuationResponse>(JsonOptions);

        // Scenario: reduce owner production to 30% (should increase valuation via higher multiple)
        var scenarioResp = await _http.PostAsJsonAsync("/api/intel/valuation/scenario", new
        {
            Overrides = new Dictionary<string, string> { ["OwnerProductionPct"] = "30" },
        });

        scenarioResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var scenario = await scenarioResp.Content.ReadFromJsonAsync<ValuationResponse>(JsonOptions);
        scenario.Should().NotBeNull();

        // Lower owner production should yield higher base (higher multiple)
        scenario!.Base.Should().BeGreaterThan(baseline!.Base);
    }

    [Fact]
    public async Task ScenarioValuation_EmptyOverrides_ReturnsBadRequest()
    {
        var jwt = await AuthenticateAsync($"intel-val-scenbad-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/valuation/scenario", new
        {
            Overrides = new Dictionary<string, string>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ComputeValuation_SignificantChange_QueuesNotification()
    {
        var jwt = await AuthenticateAsync($"intel-val-notif-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var userId = ExtractUserId(jwt);

        await SeedBenchmarkDataAsync("general-dentistry", "national", "small");

        // Ensure notification table exists
        await EnsureNotifTableAsync();

        // First compute — establishes baseline
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string> { ["AnnualRevenue"] = "900000", ["ProfitMargin"] = "20" },
        });
        await _http.PostAsync("/api/intel/valuation/compute", null);

        // Second compute with significantly different data (override via new contribution)
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string> { ["AnnualRevenue"] = "1500000", ["ProfitMargin"] = "30" },
        });
        await _http.PostAsync("/api/intel/valuation/compute", null);

        // Check that a notification was created
        var notifResponse = await _dynamo.Client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Notifications,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Notification}{userId}"),
            },
        });

        notifResponse.Items.Should().Contain(item =>
            item.ContainsKey("Type") && item["Type"].S == "ValuationChanged");
    }

    [Fact]
    public async Task ComputeValuation_NoAuth_ReturnsUnauthorized()
    {
        _http.DefaultRequestHeaders.Authorization = null;

        var response = await _http.PostAsync("/api/intel/valuation/compute", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ComputeValuation_MissingData_WiderRangeLowerConfidence()
    {
        var jwt = await AuthenticateAsync($"intel-val-missing-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await SeedBenchmarkDataAsync("general-dentistry", "national", "small");

        // Minimal data — only one category
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string> { ["AnnualRevenue"] = "800000" },
        });

        var response = await _http.PostAsync("/api/intel/valuation/compute", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ValuationResponse>(JsonOptions);
        body.Should().NotBeNull();

        // With minimal data, confidence should be lower
        body!.ConfidenceScore.Should().BeLessThan(70);

        // Range should still be valid: downside < base < upside
        body.Downside.Should().BeLessThan(body.Base);
        body.Base.Should().BeLessThan(body.Upside);
    }

    // ── Market Intelligence Tests ────────────────────────────────

    [Fact]
    public async Task GetMarketProfile_WithSeedData_ReturnsProfile()
    {
        var jwt = await AuthenticateAsync($"intel-market-get-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await SeedMarketDataAsync("us-tx-dallas", "Dallas-Fort Worth, TX",
            62.3m, 847, 0.72m,
            new[] { ("PopulationGrowthRate", 1.8m, "pct_annual", "up"), ("MedianHouseholdIncome", 72000m, "usd", "up") },
            new[] { ("HygienistAvailability", 3.2m, "per_10k_pop"), ("AssistantAvailability", 5.1m, "per_10k_pop") });

        var response = await _http.GetAsync("/api/intel/market/us-tx-dallas");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MarketProfileResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.GeoId.Should().Be("us-tx-dallas");
        body.GeoName.Should().Be("Dallas-Fort Worth, TX");
        body.PractitionerDensity.Should().Be(62.3m);
        body.CompetitorCount.Should().Be(847);
        body.ConsolidationPressure.Should().Be(0.72m);
        body.DemographicTrends.Should().HaveCount(2);
        body.DemographicTrends.Should().Contain(d => d.Name == "PopulationGrowthRate" && d.Direction == "up");
        body.WorkforceIndicators.Should().HaveCount(2);
        body.WorkforceIndicators.Should().Contain(w => w.Name == "HygienistAvailability");
    }

    [Fact]
    public async Task GetMarketProfile_UnknownGeo_ReturnsNotFound()
    {
        var jwt = await AuthenticateAsync($"intel-market-nf-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/market/nonexistent-geo");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMarketProfile_NoAuth_ReturnsUnauthorized()
    {
        _http.DefaultRequestHeaders.Authorization = null;

        var response = await _http.GetAsync("/api/intel/market/us-tx-dallas");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompareMarkets_TwoGeos_ReturnsBothProfiles()
    {
        var jwt = await AuthenticateAsync($"intel-market-cmp-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await SeedMarketDataAsync("us-oh-columbus", "Columbus, OH",
            51.7m, 412, 0.45m,
            new[] { ("PopulationGrowthRate", 1.2m, "pct_annual", "up") },
            new[] { ("HygienistAvailability", 3.8m, "per_10k_pop") });

        await SeedMarketDataAsync("us-fl-miami", "Miami-Dade, FL",
            55.4m, 623, 0.63m,
            new[] { ("PopulationGrowthRate", 1.5m, "pct_annual", "up") },
            new[] { ("HygienistAvailability", 2.9m, "per_10k_pop") });

        var response = await _http.GetAsync("/api/intel/market/compare?geos=us-oh-columbus,us-fl-miami");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MarketCompareResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Markets.Should().HaveCount(2);
        body.Markets.Should().Contain(m => m.GeoId == "us-oh-columbus");
        body.Markets.Should().Contain(m => m.GeoId == "us-fl-miami");

        // Verify different data
        var columbus = body.Markets.First(m => m.GeoId == "us-oh-columbus");
        var miami = body.Markets.First(m => m.GeoId == "us-fl-miami");
        columbus.CompetitorCount.Should().BeLessThan(miami.CompetitorCount);
    }

    [Fact]
    public async Task CompareMarkets_MissingGeos_ReturnsBadRequest()
    {
        var jwt = await AuthenticateAsync($"intel-market-cmpbad-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/market/compare?geos=only-one");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CompareMarkets_NoGeosParam_ReturnsBadRequest()
    {
        var jwt = await AuthenticateAsync($"intel-market-cmpno-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/market/compare");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task SeedMarketDataAsync(string geoId, string geoName,
        decimal practitionerDensity, int competitorCount, decimal consolidationPressure,
        (string Name, decimal Value, string Unit, string Direction)[] demographics,
        (string Name, decimal Value, string Unit)[] workforce)
    {
        var now = DateTime.UtcNow.ToString("O");

        // Profile item
        await _dynamo.Client.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Intelligence,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Market}{geoId}"),
                ["SK"] = new("PROFILE"),
                ["GeoId"] = new(geoId),
                ["GeoName"] = new(geoName),
                ["PractitionerDensity"] = new() { N = practitionerDensity.ToString("F2") },
                ["CompetitorCount"] = new() { N = competitorCount.ToString() },
                ["ConsolidationPressure"] = new() { N = consolidationPressure.ToString("F2") },
                ["ComputedAt"] = new(now),
            },
        });

        foreach (var (name, value, unit, direction) in demographics)
        {
            await _dynamo.Client.PutItemAsync(new PutItemRequest
            {
                TableName = TableNames.Intelligence,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"{KeyPrefixes.Market}{geoId}"),
                    ["SK"] = new($"DEMO#{name}"),
                    ["Name"] = new(name),
                    ["Value"] = new() { N = value.ToString("F2") },
                    ["Unit"] = new(unit),
                    ["Direction"] = new(direction),
                },
            });
        }

        foreach (var (name, value, unit) in workforce)
        {
            await _dynamo.Client.PutItemAsync(new PutItemRequest
            {
                TableName = TableNames.Intelligence,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"{KeyPrefixes.Market}{geoId}"),
                    ["SK"] = new($"WORKFORCE#{name}"),
                    ["Name"] = new(name),
                    ["Value"] = new() { N = value.ToString("F2") },
                    ["Unit"] = new(unit),
                },
            });
        }
    }

    private async Task<string?> AuthenticateAsync(string email)
    {
        try
        {
            HttpResponseMessage magicResp;
            for (var attempt = 0; ; attempt++)
            {
                magicResp = await _http.PostAsJsonAsync("/api/auth/magic-link", new { Email = email });
                if (magicResp.StatusCode != HttpStatusCode.TooManyRequests || attempt >= 3)
                    break;
                await Task.Delay(1000 * (attempt + 1));
            }
            if (!magicResp.IsSuccessStatusCode) return null;

            var code = await _fixture.PapercutClient.ExtractMagicLinkCodeAsync(email);
            if (code is null) return null;

            HttpResponseMessage validateResp;
            for (var attempt = 0; ; attempt++)
            {
                validateResp = await _http.PostAsJsonAsync("/api/auth/validate", new { Code = code });
                if (validateResp.StatusCode != HttpStatusCode.TooManyRequests || attempt >= 3)
                    break;
                await Task.Delay(1000 * (attempt + 1));
            }
            if (!validateResp.IsSuccessStatusCode) return null;

            var tokenBody = await validateResp.Content.ReadFromJsonAsync<JsonElement>();
            return tokenBody.GetProperty("accessToken").GetString();
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractUserId(string jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        return token.Subject;
    }

    private async Task EnsureNotifTableAsync()
    {
        try
        {
            await _dynamo.Client.DescribeTableAsync(TableNames.Notifications);
        }
        catch (ResourceNotFoundException)
        {
            await _dynamo.Client.CreateTableAsync(new CreateTableRequest
            {
                TableName = TableNames.Notifications,
                KeySchema =
                [
                    new KeySchemaElement("PK", KeyType.HASH),
                    new KeySchemaElement("SK", KeyType.RANGE),
                ],
                AttributeDefinitions =
                [
                    new AttributeDefinition("PK", ScalarAttributeType.S),
                    new AttributeDefinition("SK", ScalarAttributeType.S),
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            });
        }
    }

    private async Task EnsureIntelTableAsync()
    {
        try
        {
            await _dynamo.Client.DescribeTableAsync(TableNames.Intelligence);
        }
        catch (ResourceNotFoundException)
        {
            await _dynamo.Client.CreateTableAsync(new CreateTableRequest
            {
                TableName = TableNames.Intelligence,
                KeySchema =
                [
                    new KeySchemaElement("PK", KeyType.HASH),
                    new KeySchemaElement("SK", KeyType.RANGE),
                ],
                AttributeDefinitions =
                [
                    new AttributeDefinition("PK", ScalarAttributeType.S),
                    new AttributeDefinition("SK", ScalarAttributeType.S),
                    new AttributeDefinition("GSI1PK", ScalarAttributeType.S),
                    new AttributeDefinition("GSI1SK", ScalarAttributeType.S),
                    new AttributeDefinition("GSI2PK", ScalarAttributeType.S),
                    new AttributeDefinition("GSI2SK", ScalarAttributeType.S),
                ],
                GlobalSecondaryIndexes =
                [
                    new GlobalSecondaryIndex
                    {
                        IndexName = GsiNames.BenchmarkLookup,
                        KeySchema =
                        [
                            new KeySchemaElement("GSI1PK", KeyType.HASH),
                            new KeySchemaElement("GSI1SK", KeyType.RANGE),
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                    new GlobalSecondaryIndex
                    {
                        IndexName = GsiNames.RiskFlags,
                        KeySchema =
                        [
                            new KeySchemaElement("GSI2PK", KeyType.HASH),
                            new KeySchemaElement("GSI2SK", KeyType.RANGE),
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            });
        }
    }

    private async Task SeedBenchmarkDataAsync(string specialty, string geography, string sizeBand)
    {
        var benchmarks = new[]
        {
            new { Metric = "AnnualRevenue", P25 = 600000m, P50 = 850000m, P75 = 1200000m },
            new { Metric = "OverheadRatio", P25 = 55m, P50 = 62m, P75 = 70m },
            new { Metric = "CollectionsRate", P25 = 90m, P50 = 95m, P75 = 98m },
        };

        foreach (var b in benchmarks)
        {
            var pk = $"{KeyPrefixes.Cohort}dental#{specialty}#{sizeBand}";
            await _dynamo.Client.PutItemAsync(new PutItemRequest
            {
                TableName = TableNames.Intelligence,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new(pk),
                    ["SK"] = new($"METRIC#{b.Metric}"),
                    ["GSI1PK"] = new(pk),
                    ["GSI1SK"] = new($"METRIC#{b.Metric}"),
                    ["MetricName"] = new(b.Metric),
                    ["P25"] = new() { N = b.P25.ToString("F2") },
                    ["P50"] = new() { N = b.P50.ToString("F2") },
                    ["P75"] = new() { N = b.P75.ToString("F2") },
                    ["SampleSize"] = new() { N = "12" },
                    ["ComputedAt"] = new(DateTime.UtcNow.ToString("O")),
                    ["Geography"] = new(geography),
                    ["Specialty"] = new(specialty),
                    ["SizeBand"] = new(sizeBand),
                },
            });
        }
    }
}
