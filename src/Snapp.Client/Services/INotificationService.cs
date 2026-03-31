using Snapp.Shared.DTOs.Notification;
using Snapp.Shared.Models;

namespace Snapp.Client.Services;

public interface INotificationService
{
    Task<NotificationListResponse> ListAsync(int limit = 25, string? nextToken = null);
    Task<bool> MarkReadAsync(string notificationId);
    Task<bool> MarkAllReadAsync();
    Task<NotificationPreferences?> GetPreferencesAsync();
    Task<bool> SavePreferencesAsync(UpdatePreferencesRequest request);
}
