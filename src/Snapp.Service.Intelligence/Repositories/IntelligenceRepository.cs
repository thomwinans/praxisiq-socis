using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Intelligence.Repositories;

public class IntelligenceRepository : IIntelligenceRepository
{
    private readonly IAmazonDynamoDB _db;

    public IntelligenceRepository(IAmazonDynamoDB db) => _db = db;

    // ── Practice Data ────────────────────────────────────────────

    public async Task SubmitDataAsync(PracticeData data)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.PracticeData}{data.UserId}"),
            ["SK"] = new($"DIM#{data.Dimension}#{data.Category}"),
            ["UserId"] = new(data.UserId),
            ["Dimension"] = new(data.Dimension),
            ["Category"] = new(data.Category),
            ["ConfidenceContribution"] = new() { N = data.ConfidenceContribution.ToString("F4") },
            ["SubmittedAt"] = new(data.SubmittedAt.ToString("O")),
        };

        if (!string.IsNullOrEmpty(data.Source))
            item["Source"] = new(data.Source);

        // Store DataPoints as a DynamoDB map
        if (data.DataPoints.Count > 0)
        {
            item["DataPoints"] = new()
            {
                M = data.DataPoints.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new AttributeValue(kvp.Value)),
            };
        }

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Intelligence,
            Item = item,
        });
    }

    public async Task<List<PracticeData>> GetUserDataAsync(string userId)
    {
        var response = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.PracticeData}{userId}"),
                [":prefix"] = new("DIM#"),
            },
        });

        return response.Items.Select(MapPracticeData).ToList();
    }

    // ── Scoring ──────────────────────────────────────────────────

    public async Task SaveScoreAsync(string userId, Dictionary<string, decimal> dimensionScores,
        decimal overallScore, string confidenceLevel)
    {
        var now = DateTime.UtcNow;
        var timestamp = now.ToString("O");

        var scoreItem = BuildScoreItem(userId, dimensionScores, overallScore, confidenceLevel, timestamp);

        // Write CURRENT + SNAP#{timestamp} in a transaction
        var currentItem = new Dictionary<string, AttributeValue>(scoreItem)
        {
            ["PK"] = new($"{KeyPrefixes.Score}{userId}"),
            ["SK"] = new(SortKeyValues.Current),
        };

        var snapItem = new Dictionary<string, AttributeValue>(scoreItem)
        {
            ["PK"] = new($"{KeyPrefixes.Score}{userId}"),
            ["SK"] = new($"SNAP#{timestamp}"),
        };

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = TableNames.Intelligence, Item = currentItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Intelligence, Item = snapItem } },
            ],
        });
    }

    public async Task<ScoreProfile?> GetCurrentScoreAsync(string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Intelligence,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Score}{userId}"),
                ["SK"] = new(SortKeyValues.Current),
            },
        });

        return response.IsItemSet ? MapScoreProfile(response.Item) : null;
    }

    public async Task<List<ScoreProfile>> GetScoreHistoryAsync(string userId, int limit = 20)
    {
        var response = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Score}{userId}"),
                [":prefix"] = new("SNAP#"),
            },
            ScanIndexForward = false,
            Limit = Math.Min(limit, 50),
        });

        return response.Items.Select(MapScoreProfile).ToList();
    }

    // ── Valuation ────────────────────────────────────────────────

    public async Task<Valuation?> GetCurrentValuationAsync(string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Intelligence,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Valuation}{userId}"),
                ["SK"] = new(SortKeyValues.Current),
            },
        });

        return response.IsItemSet ? MapValuation(response.Item) : null;
    }

    public async Task SaveValuationAsync(Valuation valuation)
    {
        var timestamp = valuation.ComputedAt.ToString("O");
        var valItem = BuildValuationItem(valuation, timestamp);

        var currentItem = new Dictionary<string, AttributeValue>(valItem)
        {
            ["PK"] = new($"{KeyPrefixes.Valuation}{valuation.UserId}"),
            ["SK"] = new(SortKeyValues.Current),
        };

        var snapItem = new Dictionary<string, AttributeValue>(valItem)
        {
            ["PK"] = new($"{KeyPrefixes.Valuation}{valuation.UserId}"),
            ["SK"] = new($"SNAPSHOT#{timestamp}"),
        };

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = TableNames.Intelligence, Item = currentItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Intelligence, Item = snapItem } },
            ],
        });
    }

    public async Task<List<Valuation>> GetValuationHistoryAsync(string userId, int limit = 12)
    {
        var response = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Valuation}{userId}"),
                [":prefix"] = new("SNAPSHOT#"),
            },
            ScanIndexForward = false,
            Limit = Math.Min(limit, 50),
        });

        return response.Items.Select(MapValuation).ToList();
    }

    // ── Benchmarks ───────────────────────────────────────────────

    public async Task<List<Benchmark>> GetBenchmarksAsync(string specialty, string geography, string sizeBand)
    {
        // Try cohort first: COHORT#{vertical}#{specialty}#{sizeBand}
        var cohortPk = $"{KeyPrefixes.Cohort}dental#{specialty}#{sizeBand}";
        var cohortResponse = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new(cohortPk),
                [":prefix"] = new("METRIC#"),
            },
        });

        if (cohortResponse.Items.Count > 0)
            return cohortResponse.Items.Select(MapBenchmark).ToList();

        // Fall back to geographic: BENCH#{vertical}#{geo}#{geoLevel}
        var benchPk = $"{KeyPrefixes.Benchmark}dental#{geography}#national";
        var benchResponse = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new(benchPk),
                [":prefix"] = new("METRIC#"),
            },
        });

        return benchResponse.Items.Select(MapBenchmark).ToList();
    }

    public async Task SaveBenchmarkAsync(Benchmark benchmark)
    {
        var pk = !string.IsNullOrEmpty(benchmark.Specialty) && !string.IsNullOrEmpty(benchmark.SizeBand)
            ? $"{KeyPrefixes.Cohort}{benchmark.Vertical ?? "dental"}#{benchmark.Specialty}#{benchmark.SizeBand}"
            : $"{KeyPrefixes.Benchmark}{benchmark.Vertical ?? "dental"}#{benchmark.Geography}#{benchmark.GeographicLevel ?? "national"}";

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new(pk),
            ["SK"] = new($"METRIC#{benchmark.MetricName}"),
            ["MetricName"] = new(benchmark.MetricName),
            ["P25"] = new() { N = benchmark.P25.ToString("F2") },
            ["P50"] = new() { N = benchmark.P50.ToString("F2") },
            ["P75"] = new() { N = benchmark.P75.ToString("F2") },
            ["SampleSize"] = new() { N = benchmark.SampleSize.ToString() },
            ["ComputedAt"] = new(benchmark.ComputedAt.ToString("O")),
            ["Geography"] = new(benchmark.Geography),
        };

        if (benchmark.Mean.HasValue)
            item["Mean"] = new() { N = benchmark.Mean.Value.ToString("F2") };
        if (!string.IsNullOrEmpty(benchmark.Specialty))
            item["Specialty"] = new(benchmark.Specialty);
        if (!string.IsNullOrEmpty(benchmark.SizeBand))
            item["SizeBand"] = new(benchmark.SizeBand);
        if (!string.IsNullOrEmpty(benchmark.Vertical))
            item["Vertical"] = new(benchmark.Vertical);
        if (!string.IsNullOrEmpty(benchmark.GeographicLevel))
            item["GeographicLevel"] = new(benchmark.GeographicLevel);

        // GSI projection for benchmark lookups
        item["GSI1PK"] = new(pk);
        item["GSI1SK"] = new($"METRIC#{benchmark.MetricName}");

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Intelligence,
            Item = item,
        });
    }

    /// <summary>
    /// Counts distinct contributors for a given cohort (specialty + sizeBand).
    /// Used to determine if we have enough data for benchmark computation (minimum 5).
    /// </summary>
    public async Task<int> CountCohortContributorsAsync(string specialty, string sizeBand)
    {
        // Scan PDATA# items and filter by category matching the specialty
        // In production this would use a GSI; for now we count distinct users
        var response = await _db.ScanAsync(new ScanRequest
        {
            TableName = TableNames.Intelligence,
            FilterExpression = "begins_with(PK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":prefix"] = new(KeyPrefixes.PracticeData),
            },
            ProjectionExpression = "PK",
        });

        var distinctUsers = response.Items
            .Select(item => item["PK"].S.Replace(KeyPrefixes.PracticeData, ""))
            .Distinct()
            .Count();

        return distinctUsers;
    }

    /// <summary>
    /// Gets all contributed data across all users for benchmark recomputation.
    /// </summary>
    public async Task<List<PracticeData>> GetAllContributedDataAsync()
    {
        var response = await _db.ScanAsync(new ScanRequest
        {
            TableName = TableNames.Intelligence,
            FilterExpression = "begins_with(PK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":prefix"] = new(KeyPrefixes.PracticeData),
            },
        });

        return response.Items.Select(MapPracticeData).ToList();
    }

    // ── Career Stage ─────────────────────────────────────────────

    public async Task SaveCareerStageAsync(string userId, Handlers.CareerStageResult result)
    {
        var now = DateTime.UtcNow;
        var timestamp = now.ToString("O");

        var stageItem = BuildCareerStageItem(userId, result, timestamp);

        var currentItem = new Dictionary<string, AttributeValue>(stageItem)
        {
            ["PK"] = new($"{KeyPrefixes.Stage}{userId}"),
            ["SK"] = new(SortKeyValues.Current),
        };

        var snapItem = new Dictionary<string, AttributeValue>(stageItem)
        {
            ["PK"] = new($"{KeyPrefixes.Stage}{userId}"),
            ["SK"] = new($"SNAP#{timestamp}"),
        };

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = TableNames.Intelligence, Item = currentItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Intelligence, Item = snapItem } },
            ],
        });
    }

    public async Task<Endpoints.CareerStageResponse?> GetCurrentCareerStageAsync(string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Intelligence,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Stage}{userId}"),
                ["SK"] = new(SortKeyValues.Current),
            },
        });

        return response.IsItemSet ? MapCareerStage(response.Item) : null;
    }

    public async Task<List<Endpoints.CareerStageResponse>> GetCareerStageHistoryAsync(string userId, int limit = 20)
    {
        var response = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Stage}{userId}"),
                [":prefix"] = new("SNAP#"),
            },
            ScanIndexForward = false,
            Limit = Math.Min(limit, 50),
        });

        return response.Items.Select(MapCareerStage).ToList();
    }

    public async Task SaveRiskFlagsAsync(string userId, List<Handlers.RiskFlag> riskFlags)
    {
        if (riskFlags.Count == 0) return;

        var now = DateTime.UtcNow.ToString("O");

        // Delete existing risk flags for user, then write new ones
        // First get existing
        var existingResponse = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Risk}{userId}"),
            },
        });

        var transactItems = new List<TransactWriteItem>();

        // Delete existing risk flags
        foreach (var existing in existingResponse.Items)
        {
            transactItems.Add(new TransactWriteItem
            {
                Delete = new Delete
                {
                    TableName = TableNames.Intelligence,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = existing["PK"],
                        ["SK"] = existing["SK"],
                    },
                },
            });
        }

        // Add new risk flags
        foreach (var risk in riskFlags)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Risk}{userId}"),
                ["SK"] = new(risk.Type),
                ["UserId"] = new(userId),
                ["RiskType"] = new(risk.Type),
                ["Severity"] = new(risk.Severity),
                ["Description"] = new(risk.Description),
                ["DetectedAt"] = new(now),
                ["GSI2PK"] = new($"RISK#{risk.Severity}"),
                ["GSI2SK"] = new($"{risk.Type}#{userId}"),
            };
            transactItems.Add(new TransactWriteItem
            {
                Put = new Put { TableName = TableNames.Intelligence, Item = item },
            });
        }

        // DynamoDB transactions limited to 100 items
        if (transactItems.Count > 0)
        {
            await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems = transactItems,
            });
        }
    }

    // ── Private helpers ──────────────────────────────────────────

    private static Dictionary<string, AttributeValue> BuildScoreItem(
        string userId, Dictionary<string, decimal> dimensionScores,
        decimal overallScore, string confidenceLevel, string timestamp)
    {
        var dimMap = dimensionScores.ToDictionary(
            kvp => kvp.Key,
            kvp => new AttributeValue { N = kvp.Value.ToString("F2") });

        return new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new(userId),
            ["DimensionScores"] = new() { M = dimMap },
            ["OverallScore"] = new() { N = overallScore.ToString("F2") },
            ["ConfidenceLevel"] = new(confidenceLevel),
            ["ComputedAt"] = new(timestamp),
        };
    }

    private static Dictionary<string, AttributeValue> BuildValuationItem(Valuation v, string timestamp)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new(v.UserId),
            ["Downside"] = new() { N = v.Downside.ToString("F2") },
            ["Base"] = new() { N = v.Base.ToString("F2") },
            ["Upside"] = new() { N = v.Upside.ToString("F2") },
            ["ConfidenceScore"] = new() { N = v.ConfidenceScore.ToString("F2") },
            ["ComputedAt"] = new(timestamp),
        };

        if (v.Drivers.Count > 0)
        {
            item["Drivers"] = new()
            {
                M = v.Drivers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new AttributeValue(kvp.Value)),
            };
        }

        if (v.Multiple.HasValue)
            item["Multiple"] = new() { N = v.Multiple.Value.ToString("F2") };
        if (v.EbitdaMargin.HasValue)
            item["EbitdaMargin"] = new() { N = v.EbitdaMargin.Value.ToString("F4") };

        return item;
    }

    private static Dictionary<string, AttributeValue> BuildCareerStageItem(
        string userId, Handlers.CareerStageResult result, string timestamp)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new(userId),
            ["Stage"] = new(result.Stage),
            ["DisplayName"] = new(result.DisplayName),
            ["ConfidenceLevel"] = new(result.ConfidenceLevel),
            ["ComputedAt"] = new(timestamp),
        };

        if (result.TriggerSignals.Count > 0)
        {
            item["TriggerSignals"] = new() { L = result.TriggerSignals.Select(s => new AttributeValue(s)).ToList() };
        }

        if (result.RiskFlags.Count > 0)
        {
            item["RiskFlags"] = new()
            {
                L = result.RiskFlags.Select(r => new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["Type"] = new(r.Type),
                        ["Severity"] = new(r.Severity),
                        ["Description"] = new(r.Description),
                    },
                }).ToList(),
            };
        }

        return item;
    }

    private static Endpoints.CareerStageResponse MapCareerStage(Dictionary<string, AttributeValue> item) => new()
    {
        UserId = item.TryGetValue("UserId", out var uid) ? uid.S : string.Empty,
        Stage = item.TryGetValue("Stage", out var stage) ? stage.S : string.Empty,
        DisplayName = item.TryGetValue("DisplayName", out var dn) ? dn.S : string.Empty,
        ConfidenceLevel = item.TryGetValue("ConfidenceLevel", out var cl) ? cl.S : "low",
        TriggerSignals = item.TryGetValue("TriggerSignals", out var ts) && ts.L is not null
            ? ts.L.Select(a => a.S).ToList()
            : new List<string>(),
        RiskFlags = item.TryGetValue("RiskFlags", out var rf) && rf.L is not null
            ? rf.L.Select(a => new Endpoints.RiskFlagResponse
            {
                Type = a.M.TryGetValue("Type", out var t) ? t.S : string.Empty,
                Severity = a.M.TryGetValue("Severity", out var s) ? s.S : "medium",
                Description = a.M.TryGetValue("Description", out var d) ? d.S : string.Empty,
            }).ToList()
            : new List<Endpoints.RiskFlagResponse>(),
        ComputedAt = item.TryGetValue("ComputedAt", out var ca) ? DateTime.Parse(ca.S) : DateTime.MinValue,
    };

    private static PracticeData MapPracticeData(Dictionary<string, AttributeValue> item) => new()
    {
        UserId = item.TryGetValue("UserId", out var uid) ? uid.S : string.Empty,
        Dimension = item.TryGetValue("Dimension", out var dim) ? dim.S : string.Empty,
        Category = item.TryGetValue("Category", out var cat) ? cat.S : string.Empty,
        DataPoints = item.TryGetValue("DataPoints", out var dp) && dp.M is not null
            ? dp.M.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.S ?? kvp.Value.N ?? string.Empty)
            : new Dictionary<string, string>(),
        ConfidenceContribution = item.TryGetValue("ConfidenceContribution", out var cc)
            ? decimal.Parse(cc.N) : 0m,
        SubmittedAt = item.TryGetValue("SubmittedAt", out var sa)
            ? DateTime.Parse(sa.S) : DateTime.MinValue,
        Source = item.TryGetValue("Source", out var src) ? src.S : null,
    };

    private static Benchmark MapBenchmark(Dictionary<string, AttributeValue> item) => new()
    {
        MetricName = item.TryGetValue("MetricName", out var mn) ? mn.S : string.Empty,
        P25 = item.TryGetValue("P25", out var p25) ? decimal.Parse(p25.N) : 0m,
        P50 = item.TryGetValue("P50", out var p50) ? decimal.Parse(p50.N) : 0m,
        P75 = item.TryGetValue("P75", out var p75) ? decimal.Parse(p75.N) : 0m,
        Mean = item.TryGetValue("Mean", out var mean) ? decimal.Parse(mean.N) : null,
        SampleSize = item.TryGetValue("SampleSize", out var ss) ? int.Parse(ss.N) : 0,
        ComputedAt = item.TryGetValue("ComputedAt", out var ca) ? DateTime.Parse(ca.S) : DateTime.MinValue,
        Geography = item.TryGetValue("Geography", out var geo) ? geo.S : string.Empty,
        Specialty = item.TryGetValue("Specialty", out var sp) ? sp.S : null,
        SizeBand = item.TryGetValue("SizeBand", out var sb) ? sb.S : null,
        Vertical = item.TryGetValue("Vertical", out var vert) ? vert.S : null,
        GeographicLevel = item.TryGetValue("GeographicLevel", out var gl) ? gl.S : null,
    };

    private static Valuation MapValuation(Dictionary<string, AttributeValue> item) => new()
    {
        UserId = item.TryGetValue("UserId", out var uid) ? uid.S : string.Empty,
        Downside = item.TryGetValue("Downside", out var d) ? decimal.Parse(d.N) : 0m,
        Base = item.TryGetValue("Base", out var b) ? decimal.Parse(b.N) : 0m,
        Upside = item.TryGetValue("Upside", out var u) ? decimal.Parse(u.N) : 0m,
        ConfidenceScore = item.TryGetValue("ConfidenceScore", out var cs) ? decimal.Parse(cs.N) : 0m,
        Drivers = item.TryGetValue("Drivers", out var dr) && dr.M is not null
            ? dr.M.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.S ?? string.Empty)
            : new Dictionary<string, string>(),
        Multiple = item.TryGetValue("Multiple", out var m) ? decimal.Parse(m.N) : null,
        EbitdaMargin = item.TryGetValue("EbitdaMargin", out var em) ? decimal.Parse(em.N) : null,
        ComputedAt = item.TryGetValue("ComputedAt", out var ca) ? DateTime.Parse(ca.S) : DateTime.MinValue,
    };

    private static ScoreProfile MapScoreProfile(Dictionary<string, AttributeValue> item) => new()
    {
        UserId = item.TryGetValue("UserId", out var uid) ? uid.S : string.Empty,
        DimensionScores = item.TryGetValue("DimensionScores", out var ds) && ds.M is not null
            ? ds.M.ToDictionary(kvp => kvp.Key, kvp => decimal.Parse(kvp.Value.N))
            : new Dictionary<string, decimal>(),
        OverallScore = item.TryGetValue("OverallScore", out var os) ? decimal.Parse(os.N) : 0m,
        ConfidenceLevel = item.TryGetValue("ConfidenceLevel", out var cl) ? cl.S : "low",
        ComputedAt = item.TryGetValue("ComputedAt", out var ca) ? DateTime.Parse(ca.S) : DateTime.MinValue,
    };
}

/// <summary>
/// Internal model for scoring profiles stored in DynamoDB.
/// Not in Snapp.Shared because this is a service-internal representation.
/// </summary>
public class ScoreProfile
{
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, decimal> DimensionScores { get; set; } = new();
    public decimal OverallScore { get; set; }
    public string ConfidenceLevel { get; set; } = "low";
    public DateTime ComputedAt { get; set; }
}
