using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.DTOs.Transaction;

public class RecordOutcomeRequest
{
    [Required, MaxLength(2000)]
    public string Outcome { get; set; } = string.Empty;

    public bool Success { get; set; }
}
