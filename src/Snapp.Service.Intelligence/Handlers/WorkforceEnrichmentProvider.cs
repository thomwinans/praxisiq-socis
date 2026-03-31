using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Snapp.Shared.Constants;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Handlers;

/// <summary>
/// Queries JOBPOST# enrichment signals from snapp-intel and converts them
/// into PracticeData contributions for the Workforce scoring dimension.
/// </summary>
public class WorkforceEnrichmentProvider
{
    private readonly IAmazonDynamoDB _db;
    private readonly ILogger<WorkforceEnrichmentProvider> _logger;

    public WorkforceEnrichmentProvider(IAmazonDynamoDB db, ILogger<WorkforceEnrichmentProvider> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Looks up job posting signals for a practice identified by name, city, state
    /// and returns synthetic PracticeData for the Workforce dimension.
    /// </summary>
    public async Task<List<PracticeData>> GetWorkforceSignalsAsync(
        string userId, string practiceName, string practiceCity, string practiceState)
    {
        var practiceKey = $"{practiceName}|{practiceCity}|{practiceState}";
        var pk = $"JOBPOST#{practiceKey}";

        var response = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new(pk),
                [":prefix"] = new("ANALYSIS#"),
            },
            ScanIndexForward = false,
            Limit = 1, // Most recent analysis
        });

        if (response.Items.Count == 0)
        {
            _logger.LogDebug("No job posting signals found for practice {PracticeKey}", practiceKey);
            return [];
        }

        var item = response.Items[0];
        return ConvertToPracticeData(userId, item);
    }

    /// <summary>
    /// Looks up job posting signals by state (via GSI) and returns aggregated
    /// workforce data as synthetic PracticeData for scoring.
    /// </summary>
    public async Task<List<PracticeData>> GetWorkforceSignalsByStateAsync(string userId, string state)
    {
        var response = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = new($"JOBPOST#{state}"),
            },
            Limit = 50,
        });

        if (response.Items.Count == 0)
        {
            _logger.LogDebug("No job posting signals found for state {State}", state);
            return [];
        }

        // Aggregate across practices in the state
        var totalPressure = 0m;
        var totalFrequency = 0m;
        var chronicCount = 0;
        var totalUrgent = 0;
        var totalPostings = 0;
        var practiceCount = response.Items.Count;

        foreach (var item in response.Items)
        {
            if (item.TryGetValue("WorkforcePressureScore", out var pressure))
                totalPressure += decimal.Parse(pressure.N);
            if (item.TryGetValue("PostingFrequency", out var freq))
                totalFrequency += decimal.Parse(freq.N);
            if (item.TryGetValue("ChronicTurnoverSignal", out var chronic) && chronic.BOOL)
                chronicCount++;
            if (item.TryGetValue("UrgentPostings", out var urgent))
                totalUrgent += int.Parse(urgent.N);
            if (item.TryGetValue("TotalPostings", out var total))
                totalPostings += int.Parse(total.N);
        }

        var avgPressure = practiceCount > 0 ? totalPressure / practiceCount : 0m;
        var avgFrequency = practiceCount > 0 ? totalFrequency / practiceCount : 0m;
        var urgentRatio = totalPostings > 0 ? (decimal)totalUrgent / totalPostings * 100 : 0m;

        return
        [
            new PracticeData
            {
                UserId = userId,
                Dimension = "Workforce",
                Category = "workforce_signals",
                Source = "enrichment-jobposting",
                ConfidenceContribution = 0.08m,
                SubmittedAt = DateTime.UtcNow,
                DataPoints = new Dictionary<string, string>
                {
                    ["WorkforcePressureScore"] = avgPressure.ToString("F2"),
                    ["PostingFrequency"] = avgFrequency.ToString("F2"),
                    ["ChronicTurnoverSignal"] = (chronicCount > 0).ToString(),
                    ["UrgentPostingRatio"] = urgentRatio.ToString("F2"),
                },
            },
        ];
    }

    private static List<PracticeData> ConvertToPracticeData(string userId, Dictionary<string, AttributeValue> item)
    {
        var dataPoints = new Dictionary<string, string>();

        if (item.TryGetValue("WorkforcePressureScore", out var pressure))
            dataPoints["WorkforcePressureScore"] = pressure.N;
        if (item.TryGetValue("PostingFrequency", out var freq))
            dataPoints["PostingFrequency"] = freq.N;
        if (item.TryGetValue("ChronicTurnoverSignal", out var chronic))
            dataPoints["ChronicTurnoverSignal"] = chronic.BOOL.ToString();
        if (item.TryGetValue("UrgentPostings", out var urgent) && item.TryGetValue("TotalPostings", out var total))
        {
            var urgentCount = int.Parse(urgent.N);
            var totalCount = int.Parse(total.N);
            var ratio = totalCount > 0 ? (decimal)urgentCount / totalCount * 100 : 0m;
            dataPoints["UrgentPostingRatio"] = ratio.ToString("F2");
        }

        return
        [
            new PracticeData
            {
                UserId = userId,
                Dimension = "Workforce",
                Category = "workforce_signals",
                Source = "enrichment-jobposting",
                ConfidenceContribution = 0.08m,
                SubmittedAt = DateTime.UtcNow,
                DataPoints = dataPoints,
            },
        ];
    }
}
