using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Network;

public class CreateNetworkRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(10000)]
    public string? Charter { get; set; }

    [MaxLength(100)]
    public string? Template { get; set; }
}
