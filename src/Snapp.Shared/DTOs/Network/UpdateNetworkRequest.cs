using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Network;

public class UpdateNetworkRequest
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public string? Charter { get; set; }
}
