namespace Snapp.Shared.DTOs.Content;

public class ThreadListResponse
{
    public List<ThreadResponse> Threads { get; set; } = new();

    public string? NextToken { get; set; }
}
