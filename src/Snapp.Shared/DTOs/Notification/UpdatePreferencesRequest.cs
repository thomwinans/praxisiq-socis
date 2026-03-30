using System.ComponentModel.DataAnnotations;
using Snapp.Shared.Enums;

namespace Snapp.Shared.DTOs.Notification;

public class UpdatePreferencesRequest
{
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "DigestTime must be in HH:mm format.")]
    public string? DigestTime { get; set; }

    [MaxLength(50)]
    public string? Timezone { get; set; }

    public List<NotificationType>? ImmediateTypes { get; set; }
}
