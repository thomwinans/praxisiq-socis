namespace Snapp.Shared.DTOs.User;

public class ProfileResponse
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Specialty { get; set; }

    public string? Geography { get; set; }

    public decimal ProfileCompleteness { get; set; }

    public DateTime CreatedAt { get; set; }
}
