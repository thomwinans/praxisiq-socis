using Snapp.Client.Services;
using Snapp.Shared.DTOs.LinkedIn;

namespace Snapp.Client.Tests.Mocks;

public class MockLinkedInService : ILinkedInService
{
    public LinkedInStatusResponse Status { get; set; } = new() { IsLinked = false };
    public LinkedInAuthUrlResponse AuthUrl { get; set; } = new() { AuthorizationUrl = "https://linkedin.com/oauth/authorize?test=1" };
    public LinkedInShareResponse ShareResponse { get; set; } = new() { LinkedInPostUrl = "https://linkedin.com/posts/123" };
    public bool ShouldThrow { get; set; }

    public int GetStatusCallCount { get; private set; }
    public int UnlinkCallCount { get; private set; }
    public int ShareCallCount { get; private set; }
    public LinkedInShareRequest? LastShareRequest { get; private set; }

    public Task<LinkedInAuthUrlResponse> GetAuthUrlAsync()
    {
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(AuthUrl);
    }

    public Task<LinkedInStatusResponse> GetStatusAsync()
    {
        GetStatusCallCount++;
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(Status);
    }

    public Task UnlinkAsync()
    {
        UnlinkCallCount++;
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.CompletedTask;
    }

    public Task<LinkedInShareResponse> ShareAsync(LinkedInShareRequest request)
    {
        ShareCallCount++;
        LastShareRequest = request;
        if (ShouldThrow) throw new HttpRequestException("Mock error");
        return Task.FromResult(ShareResponse);
    }
}
