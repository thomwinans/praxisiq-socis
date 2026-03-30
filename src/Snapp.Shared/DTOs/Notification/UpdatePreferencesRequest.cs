using Snapp.Shared.Enums;

namespace Snapp.Shared.DTOs.Notification;

public class UpdatePreferencesRequest
{
    public string? DigestTime { get; set; }

    public string? Timezone { get; set; }

    public List<NotificationType>? ImmediateTypes { get; set; }
}
