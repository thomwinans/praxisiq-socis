using System.ComponentModel.DataAnnotations;
using Snapp.Shared.Enums;

namespace Snapp.Shared.Models;

public class DealRoom
{
    [Required]
    public string DealId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DealStatus Status { get; set; } = DealStatus.Active;

    public DateTime CreatedAt { get; set; }
}
