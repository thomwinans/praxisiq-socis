using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.Enums;
using Snapp.Shared.Interfaces;
using Snapp.Shared.Models;

namespace Snapp.Service.Transaction.Repositories;

public class DealRoomRepository : IDealRoomRepository
{
    private readonly IAmazonDynamoDB _db;

    public DealRoomRepository(IAmazonDynamoDB db) => _db = db;

    public async Task CreateDealRoomAsync(DealRoom dealRoom)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Deal}{dealRoom.DealId}"),
            ["SK"] = new(SortKeyValues.Meta),
            ["DealId"] = new(dealRoom.DealId),
            ["Name"] = new(dealRoom.Name),
            ["CreatedByUserId"] = new(dealRoom.CreatedByUserId),
            ["Status"] = new(dealRoom.Status.ToString()),
            ["CreatedAt"] = new(dealRoom.CreatedAt.ToString("O")),
        };

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Transactions,
            Item = item,
        });
    }

    public async Task<DealRoom?> GetDealRoomAsync(string dealId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Transactions,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Deal}{dealId}"),
                ["SK"] = new(SortKeyValues.Meta),
            },
        });

        return response.IsItemSet ? MapDealRoom(response.Item) : null;
    }

    public async Task UpdateDealRoomAsync(DealRoom dealRoom)
    {
        await _db.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableNames.Transactions,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Deal}{dealRoom.DealId}"),
                ["SK"] = new(SortKeyValues.Meta),
            },
            UpdateExpression = "SET #name = :name, #status = :status",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#name"] = "Name",
                ["#status"] = "Status",
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":name"] = new(dealRoom.Name),
                [":status"] = new(dealRoom.Status.ToString()),
            },
        });
    }

    public async Task<List<DealRoom>> ListUserDealRoomsAsync(string userId, string? nextToken)
    {
        // Query the user-deal index: PK = UDEAL#{userId}
        var request = new QueryRequest
        {
            TableName = TableNames.Transactions,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"UDEAL#{userId}"),
            },
            ScanIndexForward = false,
            Limit = 25,
        };

        if (!string.IsNullOrEmpty(nextToken))
            request.ExclusiveStartKey = DecodePageToken(nextToken);

        var response = await _db.QueryAsync(request);

        var dealRooms = new List<DealRoom>();
        foreach (var item in response.Items)
        {
            var dealId = item.GetValueOrDefault("DealId")?.S;
            if (dealId == null) continue;

            var deal = await GetDealRoomAsync(dealId);
            if (deal != null)
                dealRooms.Add(deal);
        }

        return dealRooms;
    }

    public async Task AddParticipantAsync(DealParticipant participant)
    {
        var partItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Deal}{participant.DealId}"),
            ["SK"] = new($"PART#{participant.UserId}"),
            ["DealId"] = new(participant.DealId),
            ["UserId"] = new(participant.UserId),
            ["Role"] = new(participant.Role),
            ["AddedAt"] = new(participant.AddedAt.ToString("O")),
        };

        // User-deal index item for ListUserDealRooms
        var userDealItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"UDEAL#{participant.UserId}"),
            ["SK"] = new($"{KeyPrefixes.Deal}{participant.AddedAt:O}#{participant.DealId}"),
            ["DealId"] = new(participant.DealId),
            ["UserId"] = new(participant.UserId),
            ["Role"] = new(participant.Role),
        };

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = TableNames.Transactions, Item = partItem } },
                new TransactWriteItem { Put = new Put { TableName = TableNames.Transactions, Item = userDealItem } },
            ],
        });
    }

    public async Task RemoveParticipantAsync(string dealId, string userId)
    {
        // Find and delete the user-deal index item
        var userDealQuery = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Transactions,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "DealId = :dealId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"UDEAL#{userId}"),
                [":prefix"] = new($"{KeyPrefixes.Deal}"),
                [":dealId"] = new(dealId),
            },
        });

        var transactItems = new List<TransactWriteItem>
        {
            new()
            {
                Delete = new Delete
                {
                    TableName = TableNames.Transactions,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new($"{KeyPrefixes.Deal}{dealId}"),
                        ["SK"] = new($"PART#{userId}"),
                    },
                },
            },
        };

        foreach (var item in userDealQuery.Items)
        {
            transactItems.Add(new TransactWriteItem
            {
                Delete = new Delete
                {
                    TableName = TableNames.Transactions,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = item["PK"],
                        ["SK"] = item["SK"],
                    },
                },
            });
        }

        await _db.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems = transactItems,
        });
    }

    public async Task<List<DealParticipant>> ListParticipantsAsync(string dealId)
    {
        var response = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Transactions,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Deal}{dealId}"),
                [":prefix"] = new("PART#"),
            },
        });

        return response.Items.Select(MapParticipant).ToList();
    }

    public async Task<bool> IsParticipantAsync(string dealId, string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Transactions,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Deal}{dealId}"),
                ["SK"] = new($"PART#{userId}"),
            },
        });

        return response.IsItemSet;
    }

    public async Task CreateDocumentAsync(DealDocument document)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Deal}{document.DealId}"),
            ["SK"] = new($"DOC#{document.CreatedAt:O}#{document.DocumentId}"),
            ["DocumentId"] = new(document.DocumentId),
            ["DealId"] = new(document.DealId),
            ["Filename"] = new(document.Filename),
            ["S3Key"] = new(document.S3Key),
            ["UploadedByUserId"] = new(document.UploadedByUserId),
            ["Size"] = new() { N = document.Size.ToString() },
            ["CreatedAt"] = new(document.CreatedAt.ToString("O")),
        };

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Transactions,
            Item = item,
        });
    }

    public async Task<List<DealDocument>> ListDocumentsAsync(string dealId, string? nextToken)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Transactions,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Deal}{dealId}"),
                [":prefix"] = new("DOC#"),
            },
            ScanIndexForward = false,
            Limit = 50,
        };

        if (!string.IsNullOrEmpty(nextToken))
            request.ExclusiveStartKey = DecodePageToken(nextToken);

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapDocument).ToList();
    }

    public async Task<DealDocument?> GetDocumentAsync(string dealId, string documentId)
    {
        // Query with prefix filter since SK includes timestamp
        var response = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Transactions,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "DocumentId = :docId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Deal}{dealId}"),
                [":prefix"] = new("DOC#"),
                [":docId"] = new(documentId),
            },
        });

        return response.Items.Count > 0 ? MapDocument(response.Items[0]) : null;
    }

    public async Task CreateAuditEntryAsync(DealAuditEntry entry)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Deal}{entry.DealId}"),
            ["SK"] = new($"AUDIT#{entry.CreatedAt:O}#{entry.EventId}"),
            ["EventId"] = new(entry.EventId),
            ["DealId"] = new(entry.DealId),
            ["Action"] = new(entry.Action),
            ["ActorUserId"] = new(entry.ActorUserId),
            ["CreatedAt"] = new(entry.CreatedAt.ToString("O")),
        };

        if (!string.IsNullOrEmpty(entry.Details))
            item["Details"] = new(entry.Details);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Transactions,
            Item = item,
        });
    }

    public async Task<List<DealAuditEntry>> ListAuditEntriesAsync(string dealId, string? nextToken, int limit = 50)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Transactions,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Deal}{dealId}"),
                [":prefix"] = new("AUDIT#"),
            },
            ScanIndexForward = false,
            Limit = limit,
        };

        if (!string.IsNullOrEmpty(nextToken))
            request.ExclusiveStartKey = DecodePageToken(nextToken);

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapAuditEntry).ToList();
    }

    // ── Mapping helpers ─────────────────────────────────────────

    private static DealRoom MapDealRoom(Dictionary<string, AttributeValue> item) => new()
    {
        DealId = item.GetValueOrDefault("DealId")?.S ?? string.Empty,
        Name = item.GetValueOrDefault("Name")?.S ?? string.Empty,
        CreatedByUserId = item.GetValueOrDefault("CreatedByUserId")?.S ?? string.Empty,
        Status = Enum.TryParse<DealStatus>(item.GetValueOrDefault("Status")?.S, out var s) ? s : DealStatus.Active,
        CreatedAt = DateTime.TryParse(item.GetValueOrDefault("CreatedAt")?.S, out var ca) ? ca : DateTime.UtcNow,
    };

    private static DealParticipant MapParticipant(Dictionary<string, AttributeValue> item) => new()
    {
        DealId = item.GetValueOrDefault("DealId")?.S ?? string.Empty,
        UserId = item.GetValueOrDefault("UserId")?.S ?? string.Empty,
        Role = item.GetValueOrDefault("Role")?.S ?? string.Empty,
        AddedAt = DateTime.TryParse(item.GetValueOrDefault("AddedAt")?.S, out var ca) ? ca : DateTime.UtcNow,
    };

    private static DealDocument MapDocument(Dictionary<string, AttributeValue> item) => new()
    {
        DocumentId = item.GetValueOrDefault("DocumentId")?.S ?? string.Empty,
        DealId = item.GetValueOrDefault("DealId")?.S ?? string.Empty,
        Filename = item.GetValueOrDefault("Filename")?.S ?? string.Empty,
        S3Key = item.GetValueOrDefault("S3Key")?.S ?? string.Empty,
        UploadedByUserId = item.GetValueOrDefault("UploadedByUserId")?.S ?? string.Empty,
        Size = long.TryParse(item.GetValueOrDefault("Size")?.N, out var sz) ? sz : 0,
        CreatedAt = DateTime.TryParse(item.GetValueOrDefault("CreatedAt")?.S, out var ca) ? ca : DateTime.UtcNow,
    };

    private static DealAuditEntry MapAuditEntry(Dictionary<string, AttributeValue> item) => new()
    {
        EventId = item.GetValueOrDefault("EventId")?.S ?? string.Empty,
        DealId = item.GetValueOrDefault("DealId")?.S ?? string.Empty,
        Action = item.GetValueOrDefault("Action")?.S ?? string.Empty,
        ActorUserId = item.GetValueOrDefault("ActorUserId")?.S ?? string.Empty,
        Details = item.GetValueOrDefault("Details")?.S,
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
}
