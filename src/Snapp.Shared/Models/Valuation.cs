using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

public class Valuation
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public decimal Downside { get; set; }

    public decimal Base { get; set; }

    public decimal Upside { get; set; }

    [Range(0, 100)]
    public decimal ConfidenceScore { get; set; }

    public Dictionary<string, string> Drivers { get; set; } = new();

    public decimal? Multiple { get; set; }

    public decimal? EbitdaMargin { get; set; }

    public DateTime ComputedAt { get; set; }
}
