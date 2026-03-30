using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Intelligence;

public class SubmitDataRequest
{
    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required]
    public Dictionary<string, string> DataPoints { get; set; } = new();
}
