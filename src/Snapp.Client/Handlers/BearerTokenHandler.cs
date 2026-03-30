using System.Net;
using System.Net.Http.Headers;
using Snapp.Client.State;

namespace Snapp.Client.Handlers;

public class BearerTokenHandler : DelegatingHandler
{
    private readonly SnappAuthStateProvider _authState;
    private readonly IServiceProvider _services;
    private bool _refreshing;

    public BearerTokenHandler(SnappAuthStateProvider authState, IServiceProvider services)
    {
        _authState = authState;
        _services = services;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _authState.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !_refreshing)
        {
            _refreshing = true;
            try
            {
                var refreshToken = await _authState.GetRefreshTokenAsync();
                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    var authService = _services.GetRequiredService<Services.IAuthService>();
                    try
                    {
                        var tokenResponse = await authService.RefreshAsync(refreshToken);
                        await _authState.SetTokensAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken);

                        // Retry the original request with new token
                        var retryRequest = await CloneRequestAsync(request);
                        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
                        response = await base.SendAsync(retryRequest, cancellationToken);
                    }
                    catch
                    {
                        await _authState.ClearTokensAsync();
                    }
                }
            }
            finally
            {
                _refreshing = false;
            }
        }

        return response;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            var content = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);
            if (request.Content.Headers.ContentType is not null)
                clone.Content.Headers.ContentType = request.Content.Headers.ContentType;
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
