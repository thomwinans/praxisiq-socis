using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

public class UserPii
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string EncryptedEmail { get; set; } = string.Empty;

    public string? EncryptedPhone { get; set; }

    public string? EncryptedContactInfo { get; set; }

    [Required]
    public string EncryptionKeyId { get; set; } = string.Empty;
}
