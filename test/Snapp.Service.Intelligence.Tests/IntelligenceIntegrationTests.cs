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
        _http = new HttpClient { BaseAddress = new Uri(fixture.KongUrl), Timeout = TimeSpan.FromSeconds(15) };
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

    // ── Helpers ──────────────────────────────────────────────────

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
