using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Snapp.Shared.Constants;
using Snapp.Shared.Enums;
using Snapp.Shared.Interfaces;
using NotificationModel = Snapp.Shared.Models.Notification;

namespace Snapp.Service.DigestJob;

public class DigestProcessor
{
    private readonly IAmazonDynamoDB _db;
    private readonly IFieldEncryptor _encryptor;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<DigestProcessor> _logger;
    private const int ParallelBatchSize = 10;

    public DigestProcessor(
        IAmazonDynamoDB db,
        IFieldEncryptor encryptor,
        IEmailSender emailSender,
        ILogger<DigestProcessor> logger)
    {
        _db = db;
        _encryptor = encryptor;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task RunAsync(string? overrideHour = null)
    {
        var currentHour = overrideHour ?? DateTime.UtcNow.Hour.ToString("D2");
        _logger.LogInformation("Digest job started for hour {Hour}", currentHour);

        var userIds = await GetUsersForDigestHourAsync(currentHour);
        _logger.LogInformation("Found {Count} users scheduled for digest hour {Hour}", userIds.Count, currentHour);

        if (userIds.Count == 0)
            return;

        // Process in batches of 10
        for (var i = 0; i < userIds.Count; i += ParallelBatchSize)
        {
            var batch = userIds.Skip(i).Take(ParallelBatchSize);
            await Task.WhenAll(batch.Select(ProcessUserDigestAsync));
        }

        _logger.LogInformation("Digest job completed for hour {Hour}", currentHour);
    }

    private async Task ProcessUserDigestAsync(string userId)
    {
        try
        {
            var notifications = await GetUndigestedAsync(userId);
            if (notifications.Count == 0)
            {
                _logger.LogInformation("No undigested notifications for user {UserId}, skipping", userId);
                return;
            }

            var email = await GetDecryptedEmailAsync(userId);
            if (email is null)
            {
                _logger.LogWarning("No email found for user {UserId}, skipping digest", userId);
                return;
            }

            var grouped = GroupByCategory(notifications);
            var today = DateTime.UtcNow.ToString("MMMM dd, yyyy");
            var htmlBody = DigestEmailRenderer.Render(today, grouped);
            var subject = $"Your PraxisIQ Daily Digest — {today}";

            await _emailSender.SendAsync(email, subject, htmlBody);

            // Only mark as digested after successful send
            await MarkDigestedAsync(userId, notifications.Select(n => n.NotificationId).ToList());
            await RecordDigestSentAsync(userId, notifications.Count, grouped.Keys.ToList());

            _logger.LogInformation(
                "Digest sent to user {UserId}: {Count} notifications in {Categories} categories",
                userId, notifications.Count, grouped.Count);
        }
        catch (Exception ex)
        {
            // Failed email send → do NOT mark as digested (retry next cycle)
            _logger.LogError(ex, "Failed to process digest for user {UserId}", userId);
        }
    }

    public static Dictionary<string, List<NotificationModel>> GroupByCategory(List<NotificationModel> notifications)
    {
        var grouped = new Dictionary<string, List<NotificationModel>>();

        foreach (var n in notifications)
        {
            var category = n.Type switch
            {
                NotificationType.ReferralReceived => "Referrals",
                NotificationType.ReferralOutcome => "Referrals",
                NotificationType.MentionInDiscussion => "Discussions",
                NotificationType.ApplicationReceived => "Network",
                NotificationType.ApplicationDecision => "Network",
                NotificationType.NewNetworkMember => "Network",
                NotificationType.ValuationChanged => "Intelligence",
                NotificationType.MilestoneAchieved => "Intelligence",
                _ => "General",
            };

            if (!grouped.ContainsKey(category))
                grouped[category] = new List<NotificationModel>();

            grouped[category].Add(n);
        }

        return grouped;
    }

    private async Task<List<string>> GetUsersForDigestHourAsync(string digestHour)
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

    private async Task<List<NotificationModel>> GetUndigestedAsync(string userId)
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

    private async Task<string?> GetDecryptedEmailAsync(string userId)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Users,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.User}{userId}"),
                ["SK"] = new(SortKeyValues.Pii),
            },
        });

        if (response.Item is null || response.Item.Count == 0)
            return null;

        if (!response.Item.TryGetValue("EncryptedEmail", out var encryptedEmail) || encryptedEmail.S is null)
            return null;

        return await _encryptor.DecryptAsync(encryptedEmail.S);
    }

    private async Task MarkDigestedAsync(string userId, List<string> notificationIds)
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

    private async Task RecordDigestSentAsync(string userId, int count, List<string> categories)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Notifications,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Digest}{userId}"),
                ["SK"] = new($"SENT#{today}"),
                ["Count"] = new() { N = count.ToString() },
                ["Categories"] = new() { SS = categories },
                ["SentAt"] = new(DateTime.UtcNow.ToString("O")),
            },
        });
    }

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
}
