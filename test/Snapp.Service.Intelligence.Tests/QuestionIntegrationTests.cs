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
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Intelligence.Tests;

[Collection(DockerTestCollection.Name)]
public class QuestionIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private readonly HttpClient _http;
    private readonly DynamoDbTestHelper _dynamo;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public QuestionIntegrationTests(DockerTestFixture fixture)
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

    // ── GET /api/intel/questions Tests ──────────────────────────

    [Fact]
    public async Task GetQuestions_NewUser_ReturnsQuestionsForGaps()
    {
        var jwt = await AuthenticateAsync($"intel-q-new-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/questions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Questions.Should().NotBeEmpty();
        body.Questions.Count.Should().BeLessOrEqualTo(3);

        // Each question should have required fields
        foreach (var q in body.Questions)
        {
            q.QuestionId.Should().NotBeNullOrEmpty();
            q.Type.Should().NotBeNullOrEmpty();
            q.PromptText.Should().NotBeNullOrEmpty();
            q.Choices.Should().NotBeEmpty();
            q.UnlockDescription.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetQuestions_UserWithSomeData_ReturnsRelevantQuestions()
    {
        var jwt = await AuthenticateAsync($"intel-q-partial-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Contribute financial data
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string>
            {
                ["AnnualRevenue"] = "850000",
            },
        });

        var response = await _http.GetAsync("/api/intel/questions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Questions.Should().NotBeEmpty();

        // Should have questions for missing categories (owner_risk, operations, etc.)
        // or missing KPIs within financial (OverheadRatio, CollectionsRate, etc.)
        body.Questions.Should().Contain(q =>
            q.Type == "EstimateValue");
    }

    [Fact]
    public async Task GetQuestions_MaxThreeReturned()
    {
        var jwt = await AuthenticateAsync($"intel-q-max3-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/questions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        body!.Questions.Count.Should().BeLessOrEqualTo(3);
    }

    [Fact]
    public async Task GetQuestions_NoAuth_ReturnsUnauthorized()
    {
        _http.DefaultRequestHeaders.Authorization = null;

        var response = await _http.GetAsync("/api/intel/questions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/intel/questions/{id}/answer Tests ─────────────

    [Fact]
    public async Task AnswerQuestion_ValidAnswer_ReturnsAccepted()
    {
        var jwt = await AuthenticateAsync($"intel-q-ans-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Get questions first
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        questions!.Questions.Should().NotBeEmpty();

        var questionId = questions.Questions[0].QuestionId;
        var answerChoice = questions.Questions[0].Choices[0];

        // Answer the question
        var response = await _http.PostAsJsonAsync($"/api/intel/questions/{questionId}/answer", new
        {
            Answer = answerChoice,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AnswerQuestionResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.QuestionId.Should().Be(questionId);
        body.Accepted.Should().BeTrue();
        body.Progression.Should().NotBeNull();
        body.Progression.TotalAnswered.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task AnswerQuestion_EmptyAnswer_ReturnsBadRequest()
    {
        var jwt = await AuthenticateAsync($"intel-q-ansbad-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Get a question first
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        questions!.Questions.Should().NotBeEmpty();

        var questionId = questions.Questions[0].QuestionId;

        var response = await _http.PostAsJsonAsync($"/api/intel/questions/{questionId}/answer", new
        {
            Answer = "",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnswerQuestion_NonexistentQuestion_ReturnsNotFound()
    {
        var jwt = await AuthenticateAsync($"intel-q-ansnf-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.PostAsJsonAsync("/api/intel/questions/NONEXISTENT123/answer", new
        {
            Answer = "Yes",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnswerQuestion_NoAuth_ReturnsUnauthorized()
    {
        _http.DefaultRequestHeaders.Authorization = null;

        var response = await _http.PostAsJsonAsync("/api/intel/questions/any-id/answer", new
        {
            Answer = "Yes",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnswerQuestion_RemovesFromPending()
    {
        var jwt = await AuthenticateAsync($"intel-q-ansdel-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Get questions
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        questions!.Questions.Should().NotBeEmpty();

        var questionId = questions.Questions[0].QuestionId;
        var userId = ExtractUserId(jwt);

        // Answer it
        await _http.PostAsJsonAsync($"/api/intel/questions/{questionId}/answer", new
        {
            Answer = questions.Questions[0].Choices[0],
        });

        // Verify QPEND# item is removed from DynamoDB
        var pendingResp = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Intelligence,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.QuestionPending}{userId}"),
                ["SK"] = new(questionId),
            },
        });
        pendingResp.IsItemSet.Should().BeFalse();

        // Verify QANS# item exists
        var ansResp = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Intelligence,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.QuestionAnswered}{userId}"),
                ["SK"] = new(questionId),
            },
        });
        ansResp.IsItemSet.Should().BeTrue();
        ansResp.Item["Answer"].S.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnswerQuestion_StoresInDynamoDb()
    {
        var jwt = await AuthenticateAsync($"intel-q-ansdb-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var userId = ExtractUserId(jwt);

        // Get questions
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        questions!.Questions.Should().NotBeEmpty();

        var questionId = questions.Questions[0].QuestionId;

        // Answer it
        await _http.PostAsJsonAsync($"/api/intel/questions/{questionId}/answer", new
        {
            Answer = "Yes",
        });

        // Read raw DynamoDB item and verify structure
        var item = await _dynamo.Client.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Intelligence,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.QuestionAnswered}{userId}"),
                ["SK"] = new(questionId),
            },
        });

        item.IsItemSet.Should().BeTrue();
        item.Item["QuestionId"].S.Should().Be(questionId);
        item.Item["UserId"].S.Should().Be(userId);
        item.Item["Answer"].S.Should().Be("Yes");
        item.Item.Should().ContainKey("AnsweredAt");
        item.Item.Should().ContainKey("Type");
        item.Item.Should().ContainKey("Category");
    }

    // ── GET /api/intel/questions/progression Tests ──────────────

    [Fact]
    public async Task GetProgression_NoAnswers_ReturnsZeros()
    {
        var jwt = await AuthenticateAsync($"intel-q-prog0-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _http.GetAsync("/api/intel/questions/progression");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ProgressionResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.TotalAnswered.Should().Be(0);
        body.TotalUnlocks.Should().Be(0);
        body.CurrentStreak.Should().Be(0);
        body.LastAnsweredAt.Should().BeNull();
    }

    [Fact]
    public async Task GetProgression_AfterAnswering_TracksProgress()
    {
        var jwt = await AuthenticateAsync($"intel-q-prog-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Get and answer questions
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        questions!.Questions.Should().NotBeEmpty();

        // Answer first question
        var q1 = questions.Questions[0];
        await _http.PostAsJsonAsync($"/api/intel/questions/{q1.QuestionId}/answer", new
        {
            Answer = q1.Choices[0],
        });

        // Check progression
        var progResp = await _http.GetAsync("/api/intel/questions/progression");
        progResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var prog = await progResp.Content.ReadFromJsonAsync<ProgressionResponse>(JsonOptions);
        prog.Should().NotBeNull();
        prog!.TotalAnswered.Should().Be(1);
        prog.CurrentStreak.Should().Be(1);
        prog.LastAnsweredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProgression_MultipleAnswers_IncrementsStreak()
    {
        var jwt = await AuthenticateAsync($"intel-q-progstrk-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Get and answer first batch
        var questionsResp1 = await _http.GetAsync("/api/intel/questions");
        var questions1 = await questionsResp1.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        questions1!.Questions.Should().NotBeEmpty();

        foreach (var q in questions1.Questions)
        {
            await _http.PostAsJsonAsync($"/api/intel/questions/{q.QuestionId}/answer", new
            {
                Answer = q.Choices[0],
            });
        }

        var progResp = await _http.GetAsync("/api/intel/questions/progression");
        var prog = await progResp.Content.ReadFromJsonAsync<ProgressionResponse>(JsonOptions);
        prog!.TotalAnswered.Should().Be(questions1.Questions.Count);
        prog.CurrentStreak.Should().Be(questions1.Questions.Count);
    }

    [Fact]
    public async Task GetProgression_NoAuth_ReturnsUnauthorized()
    {
        _http.DefaultRequestHeaders.Authorization = null;

        var response = await _http.GetAsync("/api/intel/questions/progression");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── End-to-end: Gap Detection → Answer → Progression ────────

    [Fact]
    public async Task EndToEnd_GapDetect_Answer_Progression()
    {
        var jwt = await AuthenticateAsync($"intel-q-e2e-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Step 1: Contribute partial data to create specific gaps
        await _http.PostAsJsonAsync("/api/intel/contribute", new
        {
            Category = "financial",
            DataPoints = new Dictionary<string, string>
            {
                ["AnnualRevenue"] = "900000",
            },
        });

        // Step 2: Get questions — should suggest filling missing data
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        questionsResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        questions!.Questions.Should().NotBeEmpty();
        questions.CurrentConfidence.Should().BeGreaterThan(0);

        // Step 3: Answer all questions
        foreach (var q in questions.Questions)
        {
            var ansResp = await _http.PostAsJsonAsync($"/api/intel/questions/{q.QuestionId}/answer", new
            {
                Answer = q.Choices[0],
            });
            ansResp.StatusCode.Should().Be(HttpStatusCode.OK);

            var ansBody = await ansResp.Content.ReadFromJsonAsync<AnswerQuestionResponse>(JsonOptions);
            ansBody!.Accepted.Should().BeTrue();
        }

        // Step 4: Verify progression
        var progResp = await _http.GetAsync("/api/intel/questions/progression");
        progResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var prog = await progResp.Content.ReadFromJsonAsync<ProgressionResponse>(JsonOptions);
        prog!.TotalAnswered.Should().Be(questions.Questions.Count);
        prog.CurrentStreak.Should().BeGreaterOrEqualTo(1);
        prog.LastAnsweredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EndToEnd_PublicData_GeneratesConfirmQuestions()
    {
        var jwt = await AuthenticateAsync($"intel-q-pubdata-{Guid.NewGuid():N}@test.snapp");
        if (jwt is null) return;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var userId = ExtractUserId(jwt);

        // Seed public/enrichment data directly in DynamoDB
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

        // Get questions — should include confirm_data for public data
        var questionsResp = await _http.GetAsync("/api/intel/questions");
        questionsResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var questions = await questionsResp.Content.ReadFromJsonAsync<PendingQuestionsResponse>(JsonOptions);
        questions!.Questions.Should().NotBeEmpty();
        questions.Questions.Should().Contain(q => q.Type == "ConfirmData");
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
