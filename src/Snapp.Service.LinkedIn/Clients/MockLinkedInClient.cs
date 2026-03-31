namespace Snapp.Service.LinkedIn.Clients;

/// <summary>
/// Mock LinkedIn API client for local dev/testing.
/// Returns synthetic profile data and logs share requests.
/// </summary>
public class MockLinkedInClient : ILinkedInClient
{
    private readonly ILogger<MockLinkedInClient> _logger;

    public MockLinkedInClient(ILogger<MockLinkedInClient> logger)
    {
        _logger = logger;
    }

    public Task<(string AccessToken, int ExpiresIn)> ExchangeCodeForTokenAsync(string code, string redirectUri)
    {
        _logger.LogInformation("MockLinkedIn: Exchanging code={Code} for token (redirectUri={RedirectUri})",
            code[..Math.Min(8, code.Length)], redirectUri);

        // Return a fake access token valid for 60 days
        var fakeToken = $"mock-linkedin-token-{Guid.NewGuid():N}";
        return Task.FromResult((fakeToken, 60 * 24 * 60 * 60));
    }

    public Task<LinkedInProfile> GetProfileAsync(string accessToken)
    {
        _logger.LogInformation("MockLinkedIn: Fetching profile for token={Token}",
            accessToken[..Math.Min(16, accessToken.Length)]);

        return Task.FromResult(new LinkedInProfile
        {
            Sub = $"urn:li:person:{Guid.NewGuid():N}"[..32],
            Name = "Jane Smith, DDS",
            Headline = "Owner at Bright Smile Dental | General Dentistry",
            PhotoUrl = "https://mock-linkedin.local/photos/jane-smith.jpg",
        });
    }

    public Task<string> SharePostAsync(string accessToken, string linkedInUrn, string content)
    {
        _logger.LogInformation(
            "MockLinkedIn: Share posted by URN={Urn}, content length={Length}",
            linkedInUrn[..Math.Min(16, linkedInUrn.Length)], content.Length);

        var postId = Guid.NewGuid().ToString("N")[..12];
        return Task.FromResult($"https://www.linkedin.com/feed/update/urn:li:share:{postId}");
    }
}
