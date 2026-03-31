using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.Enums;
using Snapp.Shared.Interfaces;
using NotificationModel = Snapp.Shared.Models.Notification;
using Snapp.Shared.Models;

namespace Snapp.Service.Notification.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly IAmazonDynamoDB _db;

    public NotificationRepository(IAmazonDynamoDB db) => _db = db;

    public async Task CreateNotificationAsync(NotificationModel notification)
    {
        var timestamp = notification.CreatedAt.ToString("O");
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Notification}{notification.UserId}"),
            ["SK"] = new($"EVENT#{timestamp}#{notification.NotificationId}"),
            ["NotificationId"] = new(notification.NotificationId),
            ["UserId"] = new(notification.UserId),
            ["Type"] = new(notification.Type.ToString()),
            ["Title"] = new(notification.Title),
            ["Body"] = new(notification.Body),
            ["IsRead"] = new() { BOOL = notification.IsRead },
            ["IsDigested"] = new() { BOOL = notification.IsDigested },
            ["CreatedAt"] = new(timestamp),
        };

        if (notification.Category is not null)
            item["Category"] = new(notification.Category);
        if (notification.SourceEntityId is not null)
            item["SourceEntityId"] = new(notification.SourceEntityId);

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Notifications,
            Item = item,
        });
    }

    public async Task<List<NotificationModel>> ListUserNotificationsAsync(string userId, string? nextToken, int limit = 25)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Notifications,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Notification}{userId}"),
                [":prefix"] = new("EVENT#"),
            },
            ScanIndexForward = false,
            Limit = Math.Min(limit, 100),
        };

        if (nextToken is not null)
            request.ExclusiveStartKey = DecodeNextToken(nextToken);

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapNotification).ToList();
    }

    public async Task<int> CountUnreadAsync(string userId)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Notifications,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "IsRead = :false",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Notification}{userId}"),
                [":prefix"] = new("EVENT#"),
                [":false"] = new() { BOOL = false },
            },
            Select = Select.COUNT,
        };

        var response = await _db.QueryAsync(request);
        return response.Count;
    }

    public async Task MarkReadAsync(string userId, string notificationId)
    {
        var sk = await FindNotificationSortKeyAsync(userId, notificationId);
        if (sk is null) return;

        await _db.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableNames.Notifications,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Notification}{userId}"),
                ["SK"] = new(sk),
            },
            UpdateExpression = "SET IsRead = :true",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":true"] = new() { BOOL = true },
            },
        });
    }

    public async Task MarkAllReadAsync(string userId)
    {
        var unread = await QueryUnreadAsync(userId);
        foreach (var item in unread)
        {
            await _db.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = TableNames.Notifications,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = item["PK"],
                    ["SK"] = item["SK"],
                },
                UpdateExpression = "SET IsRead = :true",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":true"] = new() { BOOL = true },
                },
            });
        }
    }

    public async Task<List<NotificationModel>> GetUndigestedAsync(string userId)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Notifications,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "IsDigested = :false",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Notification}{userId}"),
                [":prefix"] = new("EVENT#"),
                [":false"] = new() { BOOL = false },
            },
        };

        var response = await _db.QueryAsync(request);
        return response.Items.Select(MapNotification).ToList();
    }

    public async Task MarkDigestedAsync(string userId, List<string> notificationIds)
    {
        foreach (var notifId in notificationIds)
        {
            var sk = await FindNotificationSortKeyAsync(userId, notifId);
            if (sk is null) continue;

            await _db.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = TableNames.Notifications,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"{KeyPrefixes.Notification}{userId}"),
                    ["SK"] = new(sk),
                },
                UpdateExpression = "SET IsDigested = :true",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":true"] = new() { BOOL = true },
                },
            });
        }
    }

    public async Task<NotificationPreferences?> GetPreferencesAsync(string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.NotifPrefs),
            },
        });

        if (response.Item is null || response.Item.Count == 0)
            return null;

        return MapPreferences(response.Item);
    }

    public async Task SavePreferencesAsync(NotificationPreferences prefs)
    {
        // Get old preferences to check if digest hour changed
        var oldPrefs = await GetPreferencesAsync(prefs.UserId);
        var oldHour = oldPrefs?.DigestTime?.Split(':').FirstOrDefault() ?? "07";
        var newHour = prefs.DigestTime.Split(':').First();

        var immediateTypesJson = prefs.ImmediateTypes.Select(t => new AttributeValue(t.ToString())).ToList();

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.User}{prefs.UserId}"),
            ["SK"] = new(SortKeyValues.NotifPrefs),
            ["UserId"] = new(prefs.UserId),
            ["DigestTime"] = new(prefs.DigestTime),
            ["Timezone"] = new(prefs.Timezone),
            ["ImmediateTypes"] = immediateTypesJson.Count > 0
                ? new() { L = immediateTypesJson }
                : new() { L = [] },
        };

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Users,
            Item = item,
        });

        // Update digest queue bucket if hour changed
        if (oldHour != newHour && oldPrefs is not null)
        {
            // Delete old queue entry
            try
            {
                await _db.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName = TableNames.Notifications,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new($"{KeyPrefixes.DigestQueue}{oldHour}"),
                        ["SK"] = new($"{KeyPrefixes.User}{prefs.UserId}"),
                    },
                });
            }
            catch { /* Old entry may not exist */ }
        }

        // Write new queue entry
        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Notifications,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.DigestQueue}{newHour}"),
                ["SK"] = new($"{KeyPrefixes.User}{prefs.UserId}"),
                ["Timezone"] = new(prefs.Timezone),
                ["PreferredTime"] = new(prefs.DigestTime),
            },
        });
    }

    public async Task<List<string>> GetUsersForDigestHourAsync(string digestHour)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Notifications,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.DigestQueue}{digestHour}"),
                [":prefix"] = new(KeyPrefixes.User),
            },
        };

        var response = await _db.QueryAsync(request);
        return response.Items
            .Select(item => item["SK"].S.Replace(KeyPrefixes.User, ""))
            .ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<string?> FindNotificationSortKeyAsync(string userId, string notificationId)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Notifications,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "NotificationId = :notifId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Notification}{userId}"),
                [":prefix"] = new("EVENT#"),
                [":notifId"] = new(notificationId),
            },
            Limit = 1,
        };

        var response = await _db.QueryAsync(request);
        return response.Items.FirstOrDefault()?["SK"].S;
    }

    private async Task<List<Dictionary<string, AttributeValue>>> QueryUnreadAsync(string userId)
    {
        var request = new QueryRequest
        {
            TableName = TableNames.Notifications,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "IsRead = :false",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Notification}{userId}"),
                [":prefix"] = new("EVENT#"),
                [":false"] = new() { BOOL = false },
            },
        };

        var response = await _db.QueryAsync(request);
        return response.Items;
    }

    private static NotificationModel MapNotification(Dictionary<string, AttributeValue> item) => new()
    {
        NotificationId = item.GetValueOrDefault("NotificationId")?.S ?? "",
        UserId = item.GetValueOrDefault("UserId")?.S ?? "",
        Type = Enum.TryParse<NotificationType>(item.GetValueOrDefault("Type")?.S, out var t) ? t : NotificationType.DigestSummary,
        Category = item.GetValueOrDefault("Category")?.S,
        Title = item.GetValueOrDefault("Title")?.S ?? "",
        Body = item.GetValueOrDefault("Body")?.S ?? "",
        SourceEntityId = item.GetValueOrDefault("SourceEntityId")?.S,
        IsRead = item.GetValueOrDefault("IsRead")?.BOOL ?? false,
        IsDigested = item.GetValueOrDefault("IsDigested")?.BOOL ?? false,
        CreatedAt = DateTime.TryParse(item.GetValueOrDefault("CreatedAt")?.S, out var dt) ? dt : DateTime.UtcNow,
    };

    private static NotificationPreferences MapPreferences(Dictionary<string, AttributeValue> item)
    {
        var prefs = new NotificationPreferences
        {
            UserId = item.GetValueOrDefault("UserId")?.S ?? "",
            DigestTime = item.GetValueOrDefault("DigestTime")?.S ?? "07:00",
            Timezone = item.GetValueOrDefault("Timezone")?.S ?? "America/New_York",
        };

        if (item.TryGetValue("ImmediateTypes", out var immediateTypesAttr) && immediateTypesAttr.L is not null)
        {
            prefs.ImmediateTypes = immediateTypesAttr.L
                .Where(a => a.S is not null)
                .Select(a => Enum.TryParse<NotificationType>(a.S, out var nt) ? nt : (NotificationType?)null)
                .Where(nt => nt.HasValue)
                .Select(nt => nt!.Value)
                .ToList();
        }

        return prefs;
    }

    private static Dictionary<string, AttributeValue> DecodeNextToken(string token)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        return dict.ToDictionary(kv => kv.Key, kv => new AttributeValue(kv.Value));
    }
}
