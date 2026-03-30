namespace Snapp.Shared.DTOs.Notification;

public class DigestPreviewResponse
{
    public Dictionary<string, int> Categories { get; set; } = new();

    public List<NotificationResponse> TopItems { get; set; } = new();
}
