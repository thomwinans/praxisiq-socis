using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Snapp.Shared.Constants;
using Snapp.Shared.Enums;

namespace Snapp.Shared.Hosting;

/// <summary>
/// Shared helper for writing notification items to snapp-notif table.
/// Used by any service that needs to create notifications for users
/// (e.g., Content service for @mentions, Network service for applications).
/// </summary>
public static class NotificationHelper
{
    /// <summary>
    /// Writes a notification item directly to the snapp-notif DynamoDB table.
    /// </summary>
    public static async Task CreateNotificationAsync(
        IAmazonDynamoDB db,
        string userId,
        NotificationType type,
        string title,
        string body,
        string? category = null,
        string? sourceEntityId = null)
    {
        var notificationId = Ulid.NewUlid().ToString();
        var now = DateTime.UtcNow;
        var timestamp = now.ToString("O");

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Notification}{userId}"),
            ["SK"] = new($"EVENT#{timestamp}#{notificationId}"),
            ["NotificationId"] = new(notificationId),
            ["UserId"] = new(userId),
            ["Type"] = new(type.ToString()),
            ["Title"] = new(title),
            ["Body"] = new(body),
            ["IsRead"] = new() { BOOL = false },
            ["IsDigested"] = new() { BOOL = false },
            ["CreatedAt"] = new(timestamp),
        };

        if (category is not null)
            item["Category"] = new(category);
        if (sourceEntityId is not null)
            item["SourceEntityId"] = new(sourceEntityId);

        await db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Notifications,
            Item = item,
        });
    }

    /// <summary>
    /// Maps a NotificationType to its category grouping for digest emails.
    /// </summary>
    public static string GetCategory(NotificationType type) => type switch
    {
        NotificationType.ReferralReceived or NotificationType.ReferralOutcome => "Referrals",
        NotificationType.MentionInDiscussion => "Discussions",
        NotificationType.ApplicationReceived or NotificationType.ApplicationDecision or NotificationType.NewNetworkMember => "Network",
        NotificationType.ValuationChanged or NotificationType.MilestoneAchieved => "Intelligence",
        _ => "General",
    };
}
