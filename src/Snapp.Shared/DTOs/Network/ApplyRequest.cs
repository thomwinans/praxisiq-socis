using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Network;

public class ApplyRequest
{
    [Required]
    public string NetworkId { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ApplicationText { get; set; }
}
