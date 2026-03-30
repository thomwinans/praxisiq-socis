using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

public interface INotificationRepository
{
    Task CreateNotificationAsync(Notification notification);

    Task<List<Notification>> ListUserNotificationsAsync(string userId, string? nextToken, int limit = 25);

    Task MarkReadAsync(string userId, string notificationId);

    Task<List<Notification>> GetUndigestedAsync(string userId);

    Task MarkDigestedAsync(string userId, List<string> notificationIds);

    Task<NotificationPreferences?> GetPreferencesAsync(string userId);

    Task SavePreferencesAsync(NotificationPreferences prefs);

    Task<List<string>> GetUsersForDigestHourAsync(string digestHour);
}
