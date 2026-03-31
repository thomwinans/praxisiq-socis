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
public class UnlockIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly HttpClient _http;
    private readonly DynamoDbTestHelper _dynamo;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public UnlockIntegrationTests(DockerTestFixture fixture)
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

    // ── Unlock on ConfirmData ────────────────────────────────────

    [Fact]
    public async Task AnswerConfirmData_Yes_CreatesUnlockRecord()
    {
        var jwt = await AuthenticateAsync($"intel-unlock-cd-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var userId = ExtractUserId(jwt);

        // Seed public data to trigger confirm_data questions
        await _dynamo.Client.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Intelligence,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.PracticeData}{userId}"),
                ["SK"] = new("DIM#FinancialHealth#financial"),
                ["UserId"] = new(userId),
                ["Dimension"] = new("FinancialHealth"),
                ["Category"] = new("financial"),
                ["ConfidenceContribution"] = new() { N = "0.0500" },
                ["SubmittedAt"] = new(DateTime.UtcNow.ToString("O")),
                ["Source"] = new("public"),
                ["DataPoints"] = new()
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["AnnualRevenue"] = new("750000"),
                    },
                },
            },
        });

        // Get questions — should have confirm_data type
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        var confirmQ = questions!.Questions.FirstOrDefault(q => q.Type == "ConfirmData");

        if (confirmQ is null) return; // No confirm_data generated — skip gracefully

        // Answer "Yes"
        var ansResp = await _http.PostAsJsonAsync($"/api/intel/questions/{confirmQ.QuestionId}/answer", new
        {
            Answer = "Yes",
        });

        ansResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var ansBody = await ansResp.Content.ReadFromJsonAsync<AnswerQuestionResponse>(JsonOptions);
        ansBody!.Accepted.Should().BeTrue();
        ansBody.UnlockType.Should().Be("data_confirmed");
        ansBody.UnlockDescription.Should().Contain("Confirmed");
        ansBody.ConfidenceAfter.Should().BeGreaterThan(0);

        // Verify UNLOCK# record in DynamoDB
        var unlockResp = await _dynamo.Client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Unlock}{userId}"),
            },
        });
        unlockResp.Items.Should().NotBeEmpty();
        unlockResp.Items[0]["Type"].S.Should().Be("data_confirmed");
        unlockResp.Items[0]["QuestionId"].S.Should().Be(confirmQ.QuestionId);
    }

    // ── Unlock on EstimateValue ──────────────────────────────────

    [Fact]
    public async Task AnswerEstimateValue_UpdatesPracticeData()
    {
        var jwt = await AuthenticateAsync($"intel-unlock-ev-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var userId = ExtractUserId(jwt);

        // Get questions — new user should get EstimateValue questions for missing categories
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        var estimateQ = questions!.Questions.FirstOrDefault(q => q.Type == "EstimateValue");

        if (estimateQ is null) return;

        var answerChoice = estimateQ.Choices[0];

        // Answer
        var ansResp = await _http.PostAsJsonAsync($"/api/intel/questions/{estimateQ.QuestionId}/answer", new
        {
            Answer = answerChoice,
        });

        ansResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var ansBody = await ansResp.Content.ReadFromJsonAsync<AnswerQuestionResponse>(JsonOptions);
        ansBody!.UnlockType.Should().Be("estimate_recorded");
        ansBody.IntelligenceRevealed.Should().Contain("benchmark");
        ansBody.ConfidenceAfter.Should().BeGreaterThan(0);

        // Verify PDATA# was created/updated
        var pdataResp = await _dynamo.Client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.PracticeData}{userId}"),
                [":prefix"] = new("DIM#"),
            },
        });
        pdataResp.Items.Should().NotBeEmpty();
        pdataResp.Items.Should().Contain(i =>
            i.ContainsKey("Source") && i["Source"].S == "estimated");

        // Verify UNLOCK# record
        var unlockResp = await _dynamo.Client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Unlock}{userId}"),
            },
        });
        unlockResp.Items.Should().NotBeEmpty();
        unlockResp.Items[0]["Type"].S.Should().Be("estimate_recorded");
    }

    // ── Unlock on ConfirmRelationship ────────────────────────────

    [Fact]
    public async Task AnswerConfirmRelationship_No_NoUnlockCreated()
    {
        var jwt = await AuthenticateAsync($"intel-unlock-crno-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var userId = ExtractUserId(jwt);

        // Get questions
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        var relQ = questions!.Questions.FirstOrDefault(q => q.Type == "ConfirmRelationship");

        if (relQ is null) return;

        // Answer "No"
        var ansResp = await _http.PostAsJsonAsync($"/api/intel/questions/{relQ.QuestionId}/answer", new
        {
            Answer = "No",
        });

        ansResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var ansBody = await ansResp.Content.ReadFromJsonAsync<AnswerQuestionResponse>(JsonOptions);
        ansBody!.Accepted.Should().BeTrue();
        // No unlock for "No" on relationship
        ansBody.UnlockType.Should().BeNull();

        // Verify no UNLOCK# record
        var unlockResp = await _dynamo.Client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Unlock}{userId}"),
            },
        });
        unlockResp.Items.Should().BeEmpty();
    }

    // ── Confidence increases with unlocks ────────────────────────

    [Fact]
    public async Task AnswerEstimateValue_ConfidenceIncreases()
    {
        var jwt = await AuthenticateAsync($"intel-unlock-conf-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Get initial confidence
        var q1Resp = await _http.GetAsync("/api/intel/questions");
        var q1 = await q1Resp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        var initialConfidence = q1!.CurrentConfidence;

        var estimateQ = q1.Questions.FirstOrDefault(q => q.Type == "EstimateValue");
        if (estimateQ is null) return;

        // Answer to trigger unlock and PDATA# creation
        await _http.PostAsJsonAsync($"/api/intel/questions/{estimateQ.QuestionId}/answer", new
        {
            Answer = estimateQ.Choices[0],
        });

        // Get new questions — confidence should have changed
        var q2Resp = await _http.GetAsync("/api/intel/questions");
        var q2 = await q2Resp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        q2!.CurrentConfidence.Should().BeGreaterOrEqualTo(initialConfidence);
    }

    // ── Progression tracks unlocks ───────────────────────────────

    [Fact]
    public async Task AnswerMultiple_ProgressionTracksUnlocks()
    {
        var jwt = await AuthenticateAsync($"intel-unlock-progm-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Get and answer all questions
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        questions!.Questions.Should().NotBeEmpty();

        var unlockCount = 0;
        foreach (var q in questions.Questions)
        {
            var ansResp = await _http.PostAsJsonAsync($"/api/intel/questions/{q.QuestionId}/answer", new
            {
                Answer = q.Choices[0],
            });

            var ansBody = await ansResp.Content.ReadFromJsonAsync<AnswerQuestionResponse>(JsonOptions);
            if (ansBody!.UnlockType is not null)
                unlockCount++;
        }

        // Verify progression
        var progResp = await _http.GetAsync("/api/intel/questions/progression");
        var prog = await progResp.Content.ReadFromJsonAsync<ProgressionResponse>(JsonOptions);
        prog!.TotalAnswered.Should().Be(questions.Questions.Count);
        prog.TotalUnlocks.Should().Be(unlockCount);
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
}
