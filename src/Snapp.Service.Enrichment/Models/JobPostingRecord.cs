namespace Snapp.Service.Enrichment.Models;

public class JobPostingRecord
{
    public string PostingId { get; set; } = string.Empty;
    public string PracticeName { get; set; } = string.Empty;
    public string PracticeCity { get; set; } = string.Empty;
    public string PracticeState { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PostingDate { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public bool HasUrgencyLanguage { get; set; }
    public string? UrgencyPhrase { get; set; }
    public string? CompensationSignal { get; set; }
    public string Source { get; set; } = "indeed-fixture";
}

public class JobPostingAnalysis
{
    public string PracticeName { get; set; } = string.Empty;
    public string PracticeCity { get; set; } = string.Empty;
    public string PracticeState { get; set; } = string.Empty;
    public int TotalPostings { get; set; }
    public int UniqueRoles { get; set; }
    public int UrgentPostings { get; set; }
    public decimal PostingFrequency { get; set; }
    public bool ChronicTurnoverSignal { get; set; }
    public decimal WorkforcePressureScore { get; set; }
    public List<RoleRepetition> RoleRepetitions { get; set; } = [];
    public List<JobPostingRecord> Postings { get; set; } = [];
}

public class RoleRepetition
{
    public string Role { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool IsChronicTurnover { get; set; }
}
