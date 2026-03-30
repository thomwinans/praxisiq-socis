namespace Snapp.Shared.DTOs.Common;

/// <summary>
/// Generic success message response used by endpoints that return
/// a confirmation rather than a domain object (e.g., magic link sent, logout).
/// </summary>
public class MessageResponse
{
    public string Message { get; set; } = string.Empty;
}
