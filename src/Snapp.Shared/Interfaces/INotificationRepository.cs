using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

/// <summary>
/// Data access contract for the snapp-notif DynamoDB table.
/// Handles notification events, digest state, and user preferences.
/// </summary>
public interface INotificationRepository
{
    /// <summary>Creates a new notification event for a user.</summary>
    Task CreateNotificationAsync(Notification notification);

    /// <summary>Lists a user's notifications, ordered by timestamp descending. Supports pagination.</summary>
    Task<List<Notification>> ListUserNotificationsAsync(string userId, string? nextToken, int limit = 25);

    /// <summary>Marks a single notification as read.</summary>
    Task MarkReadAsync(string userId, string notificationId);

    /// <summary>Retrieves all undigested notifications for a user (IsDigested = false).</summary>
    Task<List<Notification>> GetUndigestedAsync(string userId);

    /// <summary>Marks a batch of notifications as digested after inclusion in a daily digest email.</summary>
    Task MarkDigestedAsync(string userId, List<string> notificationIds);

    /// <summary>Retrieves notification preferences for a user. Returns null if using defaults.</summary>
    Task<NotificationPreferences?> GetPreferencesAsync(string userId);

    /// <summary>Saves or updates notification preferences for a user.</summary>
    Task SavePreferencesAsync(NotificationPreferences prefs);

    /// <summary>Lists user IDs scheduled for digest delivery at the specified hour (e.g., "07").</summary>
    Task<List<string>> GetUsersForDigestHourAsync(string digestHour);
}
