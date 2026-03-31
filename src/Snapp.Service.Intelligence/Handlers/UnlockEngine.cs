using Snapp.Service.Intelligence.Endpoints;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Handlers;

/// <summary>
/// Processes intelligence unlocks triggered by question answers.
/// Maps answer types to intelligence rewards: confirm_data → market profile access,
/// confirm_relationship → connection record, estimate_value → practice data update + benchmark unlock.
/// </summary>
public class UnlockEngine
{
    private readonly IntelligenceRepository _repo;
    private readonly ScoringEngine _scoring;

    public UnlockEngine(IntelligenceRepository repo, ScoringEngine scoring)
    {
        _repo = repo;
        _scoring = scoring;
    }

    /// <summary>
    /// Processes an answered question and returns the resulting unlock.
    /// Returns null if the answer does not produce an unlock.
    /// </summary>
    public async Task<UnlockResult?> ProcessAnswerAsync(
        string userId,
        AnsweredQuestionItem answer,
        PendingQuestionItem question)
    {
        return question.Type switch
        {
            "ConfirmData" => await ProcessConfirmDataAsync(userId, answer, question),
            "ConfirmRelationship" => await ProcessConfirmRelationshipAsync(userId, answer, question),
            "EstimateValue" => await ProcessEstimateValueAsync(userId, answer, question),
            _ => null,
        };
    }

    private async Task<UnlockResult?> ProcessConfirmDataAsync(
        string userId, AnsweredQuestionItem answer, PendingQuestionItem question)
    {
        var unlockType = answer.Answer.Equals("Yes", StringComparison.OrdinalIgnoreCase)
            ? "data_confirmed"
            : "data_correction";

        var description = answer.Answer.Equals("Yes", StringComparison.OrdinalIgnoreCase)
            ? $"Confirmed! Your {question.Category} data is verified — confidence boost applied."
            : $"Thanks for the correction — we'll update your {question.Category} data.";

        string? intelligenceRevealed = null;

        // If confirmed, boost confidence by updating the source to "confirmed"
        if (answer.Answer.Equals("Yes", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(question.RelatedDataPoint))
        {
            // Re-submit the data with "confirmed" source to boost confidence
            var data = new PracticeData
            {
                UserId = userId,
                Dimension = question.Dimension,
                Category = question.Category,
                DataPoints = new Dictionary<string, string>
                {
                    [question.RelatedDataPoint] = question.RelatedValue ?? "",
                },
                ConfidenceContribution = _scoring.CalculateConfidenceContribution(question.Category),
                SubmittedAt = DateTime.UtcNow,
                Source = "confirmed",
            };
            await _repo.SubmitDataAsync(data);
            intelligenceRevealed = $"Market profile access for your geography unlocked.";
        }

        // Compute updated confidence
        var allData = await _repo.GetUserDataAsync(userId);
        var newConfidence = _scoring.CalculateTotalConfidence(allData);

        var unlock = new UnlockRecord
        {
            UnlockId = Ulid.NewUlid().ToString(),
            UserId = userId,
            QuestionId = answer.QuestionId,
            Type = unlockType,
            Description = description,
            IntelligenceRevealed = intelligenceRevealed,
            ConfidenceAfter = newConfidence,
            CreatedAt = DateTime.UtcNow,
        };
        await _repo.SaveUnlockAsync(unlock);

        return new UnlockResult
        {
            UnlockId = unlock.UnlockId,
            Type = unlockType,
            Description = description,
            IntelligenceRevealed = intelligenceRevealed,
            ConfidenceAfter = newConfidence,
        };
    }

    private async Task<UnlockResult?> ProcessConfirmRelationshipAsync(
        string userId, AnsweredQuestionItem answer, PendingQuestionItem question)
    {
        if (!answer.Answer.Equals("Yes", StringComparison.OrdinalIgnoreCase))
            return null;

        var description = "Relationship confirmed — added to your referral network. Peer comparisons unlocked.";

        // Compute updated confidence
        var allData = await _repo.GetUserDataAsync(userId);
        var newConfidence = _scoring.CalculateTotalConfidence(allData);

        var unlock = new UnlockRecord
        {
            UnlockId = Ulid.NewUlid().ToString(),
            UserId = userId,
            QuestionId = answer.QuestionId,
            Type = "relationship_confirmed",
            Description = description,
            IntelligenceRevealed = "Peer comparison data now available.",
            ConfidenceAfter = newConfidence,
            CreatedAt = DateTime.UtcNow,
        };
        await _repo.SaveUnlockAsync(unlock);

        return new UnlockResult
        {
            UnlockId = unlock.UnlockId,
            Type = "relationship_confirmed",
            Description = description,
            IntelligenceRevealed = "Peer comparison data now available.",
            ConfidenceAfter = newConfidence,
        };
    }

    private async Task<UnlockResult?> ProcessEstimateValueAsync(
        string userId, AnsweredQuestionItem answer, PendingQuestionItem question)
    {
        // Parse the answer into a data point value
        var resolvedValue = ResolveEstimateAnswer(answer.Answer, question.RelatedDataPoint);

        // Update practice data with the estimate
        if (!string.IsNullOrEmpty(question.RelatedDataPoint))
        {
            var data = new PracticeData
            {
                UserId = userId,
                Dimension = question.Dimension,
                Category = question.Category,
                DataPoints = new Dictionary<string, string>
                {
                    [question.RelatedDataPoint] = resolvedValue,
                },
                ConfidenceContribution = _scoring.CalculateConfidenceContribution(question.Category),
                SubmittedAt = DateTime.UtcNow,
                Source = "estimated",
            };
            await _repo.SubmitDataAsync(data);
        }

        // Compute updated confidence
        var allData = await _repo.GetUserDataAsync(userId);
        var newConfidence = _scoring.CalculateTotalConfidence(allData);

        var description = $"Got it! Your {question.Dimension} score has been updated with this estimate.";
        var intelligenceRevealed = $"Cohort benchmark for {question.Category} now available.";

        var unlock = new UnlockRecord
        {
            UnlockId = Ulid.NewUlid().ToString(),
            UserId = userId,
            QuestionId = answer.QuestionId,
            Type = "estimate_recorded",
            Description = description,
            IntelligenceRevealed = intelligenceRevealed,
            ConfidenceAfter = newConfidence,
            CreatedAt = DateTime.UtcNow,
        };
        await _repo.SaveUnlockAsync(unlock);

        return new UnlockResult
        {
            UnlockId = unlock.UnlockId,
            Type = "estimate_recorded",
            Description = description,
            IntelligenceRevealed = intelligenceRevealed,
            ConfidenceAfter = newConfidence,
        };
    }

    /// <summary>
    /// Resolves a human-readable estimate answer to a numeric value for storage.
    /// E.g., "$500K - $750K" → "625000", "25% - 50%" → "37.5"
    /// </summary>
    private static string ResolveEstimateAnswer(string answer, string? dataPoint)
    {
        // Revenue bands
        if (answer.Contains("$"))
        {
            return answer switch
            {
                var a when a.Contains("Under $500K") => "400000",
                var a when a.Contains("$500K - $750K") => "625000",
                var a when a.Contains("$750K - $1M") => "875000",
                var a when a.Contains("$1M - $1.5M") => "1250000",
                var a when a.Contains("Over $1.5M") => "1750000",
                var a when a.Contains("Under $100K") => "75000",
                var a when a.Contains("$100K - $250K") => "175000",
                var a when a.Contains("$250K - $500K") => "375000",
                var a when a.Contains("$500K - $1M") => "750000",
                var a when a.Contains("Over $1M") => "1250000",
                _ => "0",
            };
        }

        // Percentage bands
        if (answer.Contains("%"))
        {
            return answer switch
            {
                var a when a.Contains("Under 25%") => "15",
                var a when a.Contains("25% - 50%") => "37.5",
                var a when a.Contains("50% - 75%") => "62.5",
                var a when a.Contains("Over 75%") => "85",
                _ => "50",
            };
        }

        // Count bands
        if (answer.Contains("-") && !answer.Contains("$") && !answer.Contains("%"))
        {
            return answer switch
            {
                "0" => "0",
                "1-2" => "1.5",
                "3-5" => "4",
                "6-10" => "8",
                "3-4" => "3.5",
                "5+" => "5",
                _ => answer,
            };
        }

        // Ratio bands
        if (answer.Contains(":1"))
        {
            return answer switch
            {
                var a when a.Contains("Less than 2:1") => "1.5",
                var a when a.Contains("2:1 - 3:1") => "2.5",
                var a when a.Contains("3:1 - 4:1") => "3.5",
                var a when a.Contains("More than 4:1") => "5",
                _ => "3",
            };
        }

        // Boolean
        if (answer.Equals("Yes", StringComparison.OrdinalIgnoreCase)) return "true";
        if (answer.Equals("No", StringComparison.OrdinalIgnoreCase)) return "false";

        // Count - "More than 10"
        if (answer.Contains("More than")) return "15";

        // Fallback: Low/Medium/High
        return answer switch
        {
            "Low" => "25",
            "Medium" => "50",
            "High" => "75",
            _ => answer,
        };
    }
}

public class UnlockResult
{
    public string UnlockId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IntelligenceRevealed { get; set; }
    public decimal ConfidenceAfter { get; set; }
}

public class UnlockRecord
{
    public string UnlockId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IntelligenceRevealed { get; set; }
    public decimal ConfidenceAfter { get; set; }
    public DateTime CreatedAt { get; set; }
}
