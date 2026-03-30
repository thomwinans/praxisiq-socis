using System.ComponentModel.DataAnnotations;
using Snapp.Shared.Auth;

namespace Snapp.Shared.Models;

public class NetworkRole
{
    [Required]
    public string RoleName { get; set; } = string.Empty;

    public Permission Permissions { get; set; }

    public string? Description { get; set; }
}
