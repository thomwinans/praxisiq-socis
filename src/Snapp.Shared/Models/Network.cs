using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

public class Network
{
    [Required]
    public string NetworkId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public string? Charter { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public int MemberCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
