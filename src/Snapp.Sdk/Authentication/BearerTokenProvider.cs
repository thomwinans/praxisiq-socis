using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Snapp.Sdk.Authentication;

/// <summary>
/// Kiota authentication provider that attaches a Bearer token to all requests.
/// Accepts either a static token string or a async factory for dynamic token resolution.
/// </summary>
public sealed class BearerTokenProvider : IAuthenticationProvider
{
    private readonly Func<Task<string>> _tokenFactory;

    public BearerTokenProvider(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        _tokenFactory = () => Task.FromResult(accessToken);
    }

    public BearerTokenProvider(Func<Task<string>> tokenFactory)
    {
        ArgumentNullException.ThrowIfNull(tokenFactory);
        _tokenFactory = tokenFactory;
    }

    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        var token = await _tokenFactory();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Add("Authorization", $"Bearer {token}");
        }
    }
}
