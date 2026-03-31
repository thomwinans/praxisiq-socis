using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Snapp.Service.Intelligence.Handlers;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Constants;
using Snapp.Shared.DTOs.Common;

namespace Snapp.Service.Intelligence.Endpoints;

public static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/intel/questions", HandleGetQuestions)
            .WithName("GetPendingQuestions")
            .WithTags("Questions")
            .Produces<PendingQuestionsResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();

        app.MapPost("/api/intel/questions/{questionId}/answer", HandleAnswerQuestion)
            .WithName("AnswerQuestion")
            .WithTags("Questions")
            .Accepts<AnswerQuestionRequest>("application/json")
            .Produces<AnswerQuestionResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(401)
            .Produces<ErrorResponse>(404)
            .WithOpenApi();

        app.MapGet("/api/intel/questions/progression", HandleGetProgression)
            .WithName("GetQuestionProgression")
            .WithTags("Questions")
            .Produces<ProgressionResponse>(200)
            .Produces<ErrorResponse>(401)
            .WithOpenApi();
    }

    private static async Task<IResult> HandleGetQuestions(
        HttpRequest request,
        IntelligenceRepository repo,
        GapDetectionEngine gapEngine,
        ScoringEngine scoringEngine,
        ILogger<Program> logger)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        // Get existing user data
        var existingData = await repo.GetUserDataAsync(userId);
        var currentConfidence = scoringEngine.CalculateTotalConfidence(existingData);

        // Detect gaps and generate questions
        var generated = gapEngine.DetectGapsAndGenerateQuestions(userId, existingData, currentConfidence);

        // Check which questions are already pending (avoid duplicates)
        var existingPending = await repo.GetPendingQuestionsAsync(userId);
        var existingPrompts = existingPending.Select(q => q.PromptText).ToHashSet();

        // Filter out already-pending questions, take top 3
        var newQuestions = generated
            .Where(q => !existingPrompts.Contains(q.PromptText))
            .Take(3)
            .ToList();

        // Store as QPEND# items
        var pendingItems = new List<PendingQuestionItem>();
        foreach (var q in newQuestions)
        {
            var item = new PendingQuestionItem
            {
                QuestionId = Ulid.NewUlid().ToString(),
                UserId = userId,
                Type = q.Type.ToString(),
                Category = q.Category,
                Dimension = q.Dimension,
                PromptText = q.PromptText,
                Choices = q.Choices,
                UnlockDescription = q.UnlockDescription,
                Priority = q.Priority,
                RelatedDataPoint = q.RelatedDataPoint,
                RelatedValue = q.RelatedValue,
                CreatedAt = DateTime.UtcNow,
            };
            await repo.SavePendingQuestionAsync(item);
            pendingItems.Add(item);
        }

        // If we generated fewer than 3 new ones, backfill from existing pending
        if (pendingItems.Count < 3)
        {
            var remaining = 3 - pendingItems.Count;
            var backfill = existingPending
                .OrderByDescending(q => q.Priority)
                .Take(remaining);
            pendingItems.AddRange(backfill);
        }

        var response = new PendingQuestionsResponse
        {
            Questions = pendingItems.Take(3).Select(q => new QuestionItem
            {
                QuestionId = q.QuestionId,
                Type = q.Type,
                Category = q.Category,
                PromptText = q.PromptText,
                Choices = q.Choices,
                UnlockDescription = q.UnlockDescription,
                Priority = q.Priority,
            }).ToList(),
            CurrentConfidence = currentConfidence,
        };

        logger.LogInformation(
            "Questions generated userId={UserId}, count={Count}, confidence={Confidence}, traceId={TraceId}",
            userId, response.Questions.Count, currentConfidence, traceId);

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleAnswerQuestion(
        string questionId,
        [FromBody] AnswerQuestionRequest body,
        HttpRequest request,
        IntelligenceRepository repo,
        ScoringEngine scoringEngine,
        ILogger<Program> logger)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        if (string.IsNullOrWhiteSpace(body.Answer))
            return EndpointHelpers.BadRequest(traceId, ErrorCodes.ValidationFailed, "Answer is required.");

        // Retrieve the pending question
        var pending = await repo.GetPendingQuestionAsync(userId, questionId);
        if (pending is null)
            return EndpointHelpers.NotFound(traceId, "QUESTION_NOT_FOUND", $"Question {questionId} not found.");

        // Store QANS# item
        var answer = new AnsweredQuestionItem
        {
            QuestionId = questionId,
            UserId = userId,
            Type = pending.Type,
            Category = pending.Category,
            Dimension = pending.Dimension,
            PromptText = pending.PromptText,
            Answer = body.Answer,
            RelatedDataPoint = pending.RelatedDataPoint,
            RelatedValue = pending.RelatedValue,
            AnsweredAt = DateTime.UtcNow,
        };
        await repo.SaveAnsweredQuestionAsync(answer);

        // Remove from pending
        await repo.DeletePendingQuestionAsync(userId, questionId);

        // Update progression
        var progression = await repo.GetProgressionAsync(userId);
        progression.TotalAnswered++;
        progression.LastAnsweredAt = answer.AnsweredAt;

        // Track streak: if answered within 24h of last answer, increment streak
        if (progression.TotalAnswered == 1)
        {
            progression.CurrentStreak = 1;
        }
        else
        {
            var timeSinceLast = answer.AnsweredAt - (progression.LastStreakDate ?? DateTime.MinValue);
            if (timeSinceLast.TotalHours <= 24)
                progression.CurrentStreak++;
            else
                progression.CurrentStreak = 1;
        }
        progression.LastStreakDate = answer.AnsweredAt;

        // Process unlock: determine what was unlocked
        var unlockDescription = ProcessUnlock(pending, body.Answer);
        if (!string.IsNullOrEmpty(unlockDescription))
            progression.TotalUnlocks++;

        await repo.SaveProgressionAsync(userId, progression);

        logger.LogInformation(
            "Question answered userId={UserId}, questionId={QuestionId}, type={Type}, traceId={TraceId}",
            userId, questionId, pending.Type, traceId);

        return Results.Ok(new AnswerQuestionResponse
        {
            QuestionId = questionId,
            Accepted = true,
            UnlockDescription = unlockDescription,
            Progression = new ProgressionSummary
            {
                TotalAnswered = progression.TotalAnswered,
                TotalUnlocks = progression.TotalUnlocks,
                CurrentStreak = progression.CurrentStreak,
                LastAnsweredAt = progression.LastAnsweredAt,
            },
        });
    }

    private static async Task<IResult> HandleGetProgression(
        HttpRequest request,
        IntelligenceRepository repo)
    {
        var traceId = EndpointHelpers.NewTraceId();
        var userId = EndpointHelpers.ExtractUserId(request);
        if (userId is null) return EndpointHelpers.Unauthorized(traceId);

        var progression = await repo.GetProgressionAsync(userId);

        return Results.Ok(new ProgressionResponse
        {
            TotalAnswered = progression.TotalAnswered,
            TotalUnlocks = progression.TotalUnlocks,
            CurrentStreak = progression.CurrentStreak,
            LastAnsweredAt = progression.LastAnsweredAt,
        });
    }

    private static string? ProcessUnlock(PendingQuestionItem question, string answer)
    {
        // Determine unlock based on question type and answer
        return question.Type switch
        {
            "ConfirmData" when answer.Equals("Yes", StringComparison.OrdinalIgnoreCase) =>
                $"Confirmed! Your {question.Category} data is verified — confidence boost applied.",
            "ConfirmData" when answer.Equals("No", StringComparison.OrdinalIgnoreCase) =>
                $"Thanks for the correction — we'll update your {question.Category} data.",
            "ConfirmRelationship" when answer.Equals("Yes", StringComparison.OrdinalIgnoreCase) =>
                "Relationship confirmed — added to your referral network. Peer comparisons unlocked.",
            "EstimateValue" =>
                $"Got it! Your {question.Dimension} score has been updated with this estimate.",
            _ => null,
        };
    }
}

// ── Request/Response DTOs ────────────────────────────────────

public class PendingQuestionsResponse
{
    public List<QuestionItem> Questions { get; set; } = new();
    public decimal CurrentConfidence { get; set; }
}

public class QuestionItem
{
    public string QuestionId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public List<string> Choices { get; set; } = new();
    public string UnlockDescription { get; set; } = string.Empty;
    public decimal Priority { get; set; }
}

public class AnswerQuestionRequest
{
    public string Answer { get; set; } = string.Empty;
}

public class AnswerQuestionResponse
{
    public string QuestionId { get; set; } = string.Empty;
    public bool Accepted { get; set; }
    public string? UnlockDescription { get; set; }
    public ProgressionSummary Progression { get; set; } = new();
}

public class ProgressionResponse
{
    public int TotalAnswered { get; set; }
    public int TotalUnlocks { get; set; }
    public int CurrentStreak { get; set; }
    public DateTime? LastAnsweredAt { get; set; }
}

public class ProgressionSummary
{
    public int TotalAnswered { get; set; }
    public int TotalUnlocks { get; set; }
    public int CurrentStreak { get; set; }
    public DateTime? LastAnsweredAt { get; set; }
}

// ── Internal storage models ──────────────────────────────────

public class PendingQuestionItem
{
    public string QuestionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Dimension { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public List<string> Choices { get; set; } = new();
    public string UnlockDescription { get; set; } = string.Empty;
    public decimal Priority { get; set; }
    public string? RelatedDataPoint { get; set; }
    public string? RelatedValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AnsweredQuestionItem
{
    public string QuestionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Dimension { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? RelatedDataPoint { get; set; }
    public string? RelatedValue { get; set; }
    public DateTime AnsweredAt { get; set; }
}

public class QuestionProgression
{
    public int TotalAnswered { get; set; }
    public int TotalUnlocks { get; set; }
    public int CurrentStreak { get; set; }
    public DateTime? LastAnsweredAt { get; set; }
    public DateTime? LastStreakDate { get; set; }
}
