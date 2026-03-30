using System.ComponentModel.DataAnnotations;
using Snapp.Shared.Enums;

namespace Snapp.Shared.Models;

public class NotificationPreferences
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public string DigestTime { get; set; } = "07:00";

    public string Timezone { get; set; } = "America/New_York";

    public List<NotificationType> ImmediateTypes { get; set; } = new();
}
