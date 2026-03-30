namespace Snapp.Shared.DTOs.LinkedIn;

public class LinkedInStatusResponse
{
    public bool IsLinked { get; set; }

    public string? LinkedInName { get; set; }

    public DateTime? TokenExpiry { get; set; }
}
