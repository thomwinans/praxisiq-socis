using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

public class PracticeData
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Dimension { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public Dictionary<string, string> DataPoints { get; set; } = new();

    public decimal ConfidenceContribution { get; set; }

    public DateTime SubmittedAt { get; set; }

    public string? Source { get; set; }
}
