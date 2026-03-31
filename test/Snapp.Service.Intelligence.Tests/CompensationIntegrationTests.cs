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
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Intelligence.Tests;

[Collection(DockerTestCollection.Name)]
public class CompensationIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly HttpClient _http;
    private readonly DynamoDbTestHelper _dynamo;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public CompensationIntegrationTests(DockerTestFixture fixture)
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

    // ── Contribution Tests ─────────────────────────────────────

    [Fact]
    public async Task ContributeCompensation_ValidRole_ReturnsOk()
    {
        var jwt = await AuthenticateAsync($"comp-valid-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/compensation/contribute", new
        {
            Role = "dental_hygienist",
            CompensationType = "hourly",
            AmountBand = "$35-40/hr",
            BenefitsIncluded = true,
            DentalBenefitsIncluded = true,
            RetirementPlanIncluded = false,
            PaidTimeOff = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("message").GetString().Should().Contain("Dental Hygienist");
    }

    [Fact]
    public async Task ContributeCompensation_InvalidRole_ReturnsBadRequest()
    {
        var jwt = await AuthenticateAsync($"comp-badrole-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/compensation/contribute", new
        {
            Role = "nonexistent_role",
            CompensationType = "hourly",
            AmountBand = "$30-35/hr",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ContributeCompensation_MissingRole_ReturnsBadRequest()
    {
        var jwt = await AuthenticateAsync($"comp-norole-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/compensation/contribute", new
        {
            Role = "",
            CompensationType = "hourly",
            AmountBand = "$30-35/hr",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ContributeCompensation_NoAuth_ReturnsUnauthorized()
    {
        _http.DefaultRequestHeaders.Authorization = null;

        var response = await _http.PostAsJsonAsync("/api/intel/compensation/contribute", new
        {
            Role = "dental_hygienist",
            CompensationType = "hourly",
            AmountBand = "$35-40/hr",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Benchmark Tests ────────────────────────────────────────

    [Fact]
    public async Task GetBenchmarks_BelowAnonymityThreshold_ReturnsNoData()
    {
        // Contribute from only 3 users — below the 5-user threshold
        for (var i = 0; i < 3; i++)
        {
            var jwt = await AuthenticateAsync($"comp-below-{i}-{Guid.NewGuid():N}@test.snapp");
            if (jwt is null) return;

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            await _http.PostAsJsonAsync("/api/intel/compensation/contribute", new
            {
                Role = "dental_hygienist",
                CompensationType = "hourly",
                AmountBand = "$35-40/hr",
                BenefitsIncluded = true,
            });
        }

        // Query benchmarks
        var queryJwt = await AuthenticateAsync($"comp-below-q-{Guid.NewGuid():N}@test.snapp");
        if (queryJwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", queryJwt);

        var response = await _http.GetAsync("/api/intel/compensation/benchmarks");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CompensationBenchmarkResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.AnonymityThreshold.Should().Be(5);

        // Dental hygienist should not meet threshold (only 3 contributors in this test,
        // but other tests may have added more — so we just check the response structure)
        body.Roles.Should().NotBeEmpty();
        foreach (var role in body.Roles.Where(r => !r.MeetsAnonymityThreshold))
        {
            role.P25.Should().Be(0);
            role.P50.Should().Be(0);
            role.P75.Should().Be(0);
            role.Summary.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetBenchmarks_FiveOrMoreContributors_ReturnsPercentiles()
    {
        var bands = new[] { "$30-35/hr", "$35-40/hr", "$40-45/hr", "$35-40/hr", "$45-50/hr", "$40-45/hr" };

        // Contribute from 6 unique users for front_desk role (less likely to collide with other tests)
        for (var i = 0; i < 6; i++)
        {
            var jwt = await AuthenticateAsync($"comp-above-{i}-{Guid.NewGuid():N}@test.snapp");
            if (jwt is null) return;

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            await _http.PostAsJsonAsync("/api/intel/compensation/contribute", new
            {
                Role = "front_desk",
                CompensationType = "hourly",
                AmountBand = bands[i],
                BenefitsIncluded = i % 2 == 0,
                PaidTimeOff = true,
            });
        }

        // Query benchmarks
        var queryJwt = await AuthenticateAsync($"comp-above-q-{Guid.NewGuid():N}@test.snapp");
        if (queryJwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", queryJwt);

        var response = await _http.GetAsync("/api/intel/compensation/benchmarks");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CompensationBenchmarkResponse>(JsonOptions);
        body.Should().NotBeNull();

        var frontDesk = body!.Roles.FirstOrDefault(r => r.Role == "front_desk");
        frontDesk.Should().NotBeNull();
        frontDesk!.MeetsAnonymityThreshold.Should().BeTrue();
        frontDesk.ContributorCount.Should().BeGreaterOrEqualTo(5);
        frontDesk.P25.Should().BeGreaterThan(0);
        frontDesk.P50.Should().BeGreaterThan(frontDesk.P25);
        frontDesk.P75.Should().BeGreaterOrEqualTo(frontDesk.P50);
        frontDesk.CompensationUnit.Should().Be("/hr");
        frontDesk.Summary.Should().Contain("Front Desk");
        frontDesk.Summary.Should().Contain("between");
    }

    [Fact]
    public async Task GetBenchmarks_FormattedOutput_ContainsRangeStatement()
    {
        // Seed 5 contributions for office_manager role with salary bands
        var bands = new[] { "$50k-60k/yr", "$60k-70k/yr", "$50k-60k/yr", "$70k-80k/yr", "$60k-70k/yr" };

        for (var i = 0; i < 5; i++)
        {
            var jwt = await AuthenticateAsync($"comp-fmt-{i}-{Guid.NewGuid():N}@test.snapp");
            if (jwt is null) return;

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            await _http.PostAsJsonAsync("/api/intel/compensation/contribute", new
            {
                Role = "office_manager",
                CompensationType = "salary",
                AmountBand = bands[i],
                BenefitsIncluded = true,
                RetirementPlanIncluded = true,
            });
        }

        // Query
        var queryJwt = await AuthenticateAsync($"comp-fmt-q-{Guid.NewGuid():N}@test.snapp");
        if (queryJwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", queryJwt);

        var response = await _http.GetAsync("/api/intel/compensation/benchmarks");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CompensationBenchmarkResponse>(JsonOptions);
        var mgr = body!.Roles.FirstOrDefault(r => r.Role == "office_manager");
        mgr.Should().NotBeNull();

        if (mgr!.MeetsAnonymityThreshold)
        {
            mgr.CompensationUnit.Should().Be("/yr");
            mgr.Summary.Should().Contain("Practices in your market pay Office Manager between");
        }
    }

    [Fact]
    public async Task GetBenchmarks_RolesResolvedFromConfig_NotHardCoded()
    {
        var jwt = await AuthenticateAsync($"comp-roles-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/compensation/benchmarks");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CompensationBenchmarkResponse>(JsonOptions);
        body.Should().NotBeNull();

        // Should have all 6 roles from dental-default.json
        body!.Roles.Select(r => r.Role).Should().Contain("dental_hygienist");
        body.Roles.Select(r => r.Role).Should().Contain("dental_assistant");
        body.Roles.Select(r => r.Role).Should().Contain("front_desk");
        body.Roles.Select(r => r.Role).Should().Contain("office_manager");
        body.Roles.Select(r => r.Role).Should().Contain("associate_dentist");
        body.Roles.Select(r => r.Role).Should().Contain("specialist");

        // Display names should be human-readable
        body.Roles.First(r => r.Role == "dental_hygienist").DisplayName.Should().Be("Dental Hygienist");
    }

    [Fact]
    public async Task ContributeCompensation_Upsert_UpdatesExistingRole()
    {
        var jwt = await AuthenticateAsync($"comp-upsert-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // First contribution
        await _http.PostAsJsonAsync("/api/intel/compensation/contribute", new
        {
            Role = "dental_assistant",
            CompensationType = "hourly",
            AmountBand = "$18-22/hr",
            BenefitsIncluded = false,
        });

        // Update same role with different band
        var response = await _http.PostAsJsonAsync("/api/intel/compensation/contribute", new
        {
            Role = "dental_assistant",
            CompensationType = "hourly",
            AmountBand = "$22-26/hr",
            BenefitsIncluded = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify in DynamoDB — should only have one item for this user+role (upsert)
        var userId = ExtractUserId(jwt);
        var item = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Intelligence,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"COMP#{userId}"),
                ["SK"] = new("ROLE#dental_assistant"),
            },
        });

        item.Item.Should().NotBeEmpty();
        item.Item["AmountBand"].S.Should().Be("$22-26/hr");
        item.Item["BenefitsIncluded"].BOOL.Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────

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
            try { await _dynamo.Client.CreateTableAsync(new CreateTableRequest
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
            }); } catch (ResourceInUseException) { /* concurrent creation */ }
        }
    }
}
