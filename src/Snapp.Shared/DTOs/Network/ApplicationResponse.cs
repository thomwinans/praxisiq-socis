namespace Snapp.Shared.DTOs.Network;

public class ApplicationResponse
{
    public string UserId { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? ApplicationText { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
