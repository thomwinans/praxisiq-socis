using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Service.Transaction.Models;
using Snapp.Shared.Constants;
using Snapp.Shared.Enums;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Transaction.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly IAmazonDynamoDB _db;

    public TransactionRepository(IAmazonDynamoDB db) => _db = db;

    public async Task CreateReferralAsync(Referral referral)
    {
        var timestamp = referral.CreatedAt.ToString("O");
        var refItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Referral}{referral.ReferralId}"),
            ["SK"] = new(SortKeyValues.Meta),
            ["ReferralId"] = new(referral.ReferralId),
            ["SenderUserId"] = new(referral.SenderUserId),
            ["ReceiverUserId"] = new(referral.ReceiverUserId),
            ["NetworkId"] = new(referral.NetworkId),
            ["Status"] = new(referral.Status.ToString()),
            ["CreatedAt"] = new(timestamp),
        };

        if (!string.IsNullOrEmpty(referral.Specialty))
            refItem["Specialty"] = new(referral.Specialty);
        if (!string.IsNullOrEmpty(referral.Notes))
            refItem["Notes"] = new(referral.Notes);

        var senderIndex = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.UserReferral}{referral.SenderUserId}#SENT"),
            ["SK"] = new($"{KeyPrefixes.Referral}{timestamp}#{referral.ReferralId}"),
            ["ReferralId"] = new(referral.ReferralId),
            ["ReceiverUserId"] = new(referral.ReceiverUserId),
            ["NetworkId"] = new(referral.NetworkId),
            ["Status"] = new(referral.Status.ToString()),
            ["CreatedAt"] = new(timestamp),
        };

        var receiverIndex = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.UserReferral}{referral.ReceiverUserId}#RECV"),
            ["SK"] = new($"{KeyPrefixes.Referral}{timestamp}#{referral.ReferralId}"),
            ["ReferralId"] = new(referral.ReferralId),
            ["SenderUserId"] = new(referral.SenderUserId),
            ["NetworkId"] = new(referral.NetworkId),
            ["Status"] = new(referral.Status.ToString()),
            ["CreatedAt"] = new(timestamp),
        };

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = TableNames.Transactions, Item = refItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Transactions, Item = senderIndex } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Transactions, Item = receiverIndex } },
            ],
        });
    }

    public async Task UpdateReferralAsync(Referral referral)
    {
        var updateExpr = "SET #status = :status";
        var exprNames = new Dictionary<string, string> { ["#status"] = "Status" };
        var exprValues = new Dictionary<string, AttributeValue>
        {
            [":status"] = new(referral.Status.ToString()),
        };

        if (referral.OutcomeRecordedAt.HasValue)
        {
            updateExpr += ", OutcomeRecordedAt = :outcomeAt";
            exprValues[":outcomeAt"] = new(referral.OutcomeRecordedAt.Value.ToString("O"));
        }

        if (!string.IsNullOrEmpty(referral.Notes))
        {
            updateExpr += ", Notes = :notes";
            exprValues[":notes"] = new(referral.Notes);
        }

        await _db.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableNames.Transactions,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Referral}{referral.ReferralId}"),
                ["SK"] = new(SortKeyValues.Meta),
            },
            UpdateExpression = updateExpr,
            ExpressionAttributeNames = exprNames,
            ExpressionAttributeValues = exprValues,
        });
    }

    public async Task<Referral?> GetReferralAsync(string referralId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Transactions,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Referral}{referralId}"),
                ["SK"] = new(SortKeyValues.Meta),
            },
        });

        if (!response.IsItemSet) return null;
        return MapReferral(response.Item);
    }

    public async Task<List<Referral>> ListSentReferralsAsync(string userId, string? nextToken)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Transactions,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.UserReferral}{userId}#SENT"),
            },
            ScanIndexForward = false,
            Limit = 25,
        };

        if (!string.IsNullOrEmpty(nextToken))
            request.ExclusiveStartKey = DecodePageToken(nextToken);

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapReferralFromIndex).ToList();
    }

    public async Task<List<Referral>> ListReceivedReferralsAsync(string userId, string? nextToken)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Transactions,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.UserReferral}{userId}#RECV"),
            },
            ScanIndexForward = false,
            Limit = 25,
        };

        if (!string.IsNullOrEmpty(nextToken))
            request.ExclusiveStartKey = DecodePageToken(nextToken);

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapReferralFromIndex).ToList();
    }

    public async Task<Reputation?> GetReputationAsync(string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Transactions,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Reputation}{userId}"),
                ["SK"] = new("CURRENT"),
            },
        });

        if (!response.IsItemSet) return null;
        return MapReputation(response.Item);
    }

    public async Task SaveReputationAsync(Reputation reputation)
    {
        var timestamp = reputation.ComputedAt.ToString("O");
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new(reputation.UserId),
            ["OverallScore"] = new() { N = reputation.OverallScore.ToString("F4") },
            ["ReferralScore"] = new() { N = reputation.ReferralScore.ToString("F4") },
            ["ContributionScore"] = new() { N = reputation.ContributionScore.ToString("F4") },
            ["AttestationScore"] = new() { N = reputation.AttestationScore.ToString("F4") },
            ["ComputedAt"] = new(timestamp),
        };

        var currentItem = new Dictionary<string, AttributeValue>(attrs)
        {
            ["PK"] = new($"{KeyPrefixes.Reputation}{reputation.UserId}"),
            ["SK"] = new("CURRENT"),
        };

        var snapshotItem = new Dictionary<string, AttributeValue>(attrs)
        {
            ["PK"] = new($"{KeyPrefixes.Reputation}{reputation.UserId}"),
            ["SK"] = new($"SNAP#{timestamp}"),
        };

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = TableNames.Transactions, Item = currentItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Transactions, Item = snapshotItem } },
            ],
        });
    }

    // --- Attestation operations (not in ITransactionRepository) ---

    public async Task CreateAttestationAsync(Attestation attestation)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Attestation}{attestation.TargetUserId}"),
            ["SK"] = new($"FROM#{attestation.AttestorUserId}"),
            ["TargetUserId"] = new(attestation.TargetUserId),
            ["AttestorUserId"] = new(attestation.AttestorUserId),
            ["Skill"] = new(attestation.Skill),
            ["CreatedAt"] = new(attestation.CreatedAt.ToString("O")),
        };

        if (!string.IsNullOrEmpty(attestation.Comment))
            item["Comment"] = new(attestation.Comment);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Transactions,
            Item = item,
        });
    }

    public async Task<Attestation?> GetAttestationAsync(string targetUserId, string attestorUserId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Transactions,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Attestation}{targetUserId}"),
                ["SK"] = new($"FROM#{attestorUserId}"),
            },
        });

        if (!response.IsItemSet) return null;
        return MapAttestation(response.Item);
    }

    public async Task<List<Attestation>> ListAttestationsForUserAsync(string targetUserId)
    {
        var response = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Transactions,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Attestation}{targetUserId}"),
                [":prefix"] = new("FROM#"),
            },
        });

        return response.Items.Select(MapAttestation).ToList();
    }

    public async Task<List<Attestation>> ListAttestationsByUserAsync(string attestorUserId)
    {
        // Scan for attestations made by this user (for anti-gaming detection)
        var response = await _db.ScanAsync(new ScanRequest
        {
            TableName = TableNames.Transactions,
            FilterExpression = "AttestorUserId = :uid AND begins_with(PK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":uid"] = new(attestorUserId),
                [":prefix"] = new(KeyPrefixes.Attestation),
            },
        });

        return response.Items.Select(MapAttestation).ToList();
    }

    public async Task<List<Reputation>> ListReputationHistoryAsync(string userId, string? nextToken)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Transactions,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Reputation}{userId}"),
                [":prefix"] = new("SNAP#"),
            },
            ScanIndexForward = false,
            Limit = 25,
        };

        if (!string.IsNullOrEmpty(nextToken))
            request.ExclusiveStartKey = DecodePageToken(nextToken);

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapReputation).ToList();
    }

    public async Task<int> CountSuccessfulReferralsAsync(string userId)
    {
        var sent = await ListSentReferralsAsync(userId, null);
        return sent.Count(r => r.Status == ReferralStatus.Completed);
    }

    // --- Mapping helpers ---

    private static Referral MapReferral(Dictionary<string, AttributeValue> item) => new()
    {
        ReferralId = item.GetValueOrDefault("ReferralId")?.S ?? string.Empty,
        SenderUserId = item.GetValueOrDefault("SenderUserId")?.S ?? string.Empty,
        ReceiverUserId = item.GetValueOrDefault("ReceiverUserId")?.S ?? string.Empty,
        NetworkId = item.GetValueOrDefault("NetworkId")?.S ?? string.Empty,
        Specialty = item.GetValueOrDefault("Specialty")?.S,
        Status = Enum.TryParse<ReferralStatus>(item.GetValueOrDefault("Status")?.S, out var s) ? s : ReferralStatus.Created,
        Notes = item.GetValueOrDefault("Notes")?.S,
        CreatedAt = DateTime.TryParse(item.GetValueOrDefault("CreatedAt")?.S, out var ca) ? ca : DateTime.UtcNow,
        OutcomeRecordedAt = item.TryGetValue("OutcomeRecordedAt", out var oa) && DateTime.TryParse(oa.S, out var oaParsed) ? oaParsed : null,
    };

    private static Referral MapReferralFromIndex(Dictionary<string, AttributeValue> item) => new()
    {
        ReferralId = item.GetValueOrDefault("ReferralId")?.S ?? string.Empty,
        SenderUserId = item.GetValueOrDefault("SenderUserId")?.S ?? string.Empty,
        ReceiverUserId = item.GetValueOrDefault("ReceiverUserId")?.S ?? string.Empty,
        NetworkId = item.GetValueOrDefault("NetworkId")?.S ?? string.Empty,
        Status = Enum.TryParse<ReferralStatus>(item.GetValueOrDefault("Status")?.S, out var s) ? s : ReferralStatus.Created,
        CreatedAt = DateTime.TryParse(item.GetValueOrDefault("CreatedAt")?.S, out var ca) ? ca : DateTime.UtcNow,
    };

    private static Reputation MapReputation(Dictionary<string, AttributeValue> item) => new()
    {
        UserId = item.GetValueOrDefault("UserId")?.S ?? string.Empty,
        OverallScore = decimal.TryParse(item.GetValueOrDefault("OverallScore")?.N, out var os) ? os : 0,
        ReferralScore = decimal.TryParse(item.GetValueOrDefault("ReferralScore")?.N, out var rs) ? rs : 0,
        ContributionScore = decimal.TryParse(item.GetValueOrDefault("ContributionScore")?.N, out var cs) ? cs : 0,
        AttestationScore = decimal.TryParse(item.GetValueOrDefault("AttestationScore")?.N, out var ats) ? ats : 0,
        ComputedAt = DateTime.TryParse(item.GetValueOrDefault("ComputedAt")?.S, out var ca) ? ca : DateTime.UtcNow,
    };

    private static Attestation MapAttestation(Dictionary<string, AttributeValue> item) => new()
    {
        TargetUserId = item.GetValueOrDefault("TargetUserId")?.S ?? string.Empty,
        AttestorUserId = item.GetValueOrDefault("AttestorUserId")?.S ?? string.Empty,
        Skill = item.GetValueOrDefault("Skill")?.S ?? string.Empty,
        Comment = item.GetValueOrDefault("Comment")?.S,
        CreatedAt = DateTime.TryParse(item.GetValueOrDefault("CreatedAt")?.S, out var ca) ? ca : DateTime.UtcNow,
    };

    private static Dictionary<string, AttributeValue>? DecodePageToken(string token)
    {
        try
        {
            var bytes = Convert.FromBase64String(token);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return dict?.ToDictionary(kv => kv.Key, kv => new AttributeValue(kv.Value));
        }
        catch
        {
            return null;
        }
    }

    public static string? EncodePageToken(Dictionary<string, AttributeValue>? lastEvaluatedKey)
    {
        if (lastEvaluatedKey == null || lastEvaluatedKey.Count == 0) return null;
        var dict = lastEvaluatedKey.ToDictionary(kv => kv.Key, kv => kv.Value.S);
        var json = System.Text.Json.JsonSerializer.Serialize(dict);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }
}
