namespace Snapp.Shared.DTOs.Notification;

public class NotificationListResponse
{
    public List<NotificationResponse> Notifications { get; set; } = new();

    public int UnreadCount { get; set; }

    public string? NextToken { get; set; }
}
