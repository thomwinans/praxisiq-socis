using System.ComponentModel.DataAnnotations;
using Snapp.Shared.Enums;

namespace Snapp.Shared.Models;

public class Notification
{
    [Required]
    public string NotificationId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    public string? Category { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public string? SourceEntityId { get; set; }

    public bool IsRead { get; set; }

    public bool IsDigested { get; set; }

    public DateTime CreatedAt { get; set; }
}
