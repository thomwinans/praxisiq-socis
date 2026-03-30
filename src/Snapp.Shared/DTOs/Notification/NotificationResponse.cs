using Snapp.Shared.Enums;

namespace Snapp.Shared.DTOs.Notification;

public class NotificationResponse
{
    public string NotificationId { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    public string? Category { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}
