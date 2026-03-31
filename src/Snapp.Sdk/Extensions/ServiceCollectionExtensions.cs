using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Snapp.Sdk.Authentication;

namespace Snapp.Sdk.Extensions;

public sealed class SnappSdkOptions
{
    /// <summary>
    /// Base URL for the SNAPP API (e.g. "http://localhost:8000" or "https://api.snapp.praxisiq.com").
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8000";

    /// <summary>
    /// Async factory that returns a valid access token for each request.
    /// </summary>
    public Func<Task<string>>? TokenProvider { get; set; }

    /// <summary>
    /// Static access token. Used if TokenProvider is not set.
    /// </summary>
    public string? AccessToken { get; set; }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSnappSdk(
        this IServiceCollection services,
        Action<SnappSdkOptions> configure)
    {
        var options = new SnappSdkOptions();
        configure(options);

        services.AddHttpClient("SnappSdk")
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            });

        services.AddSingleton(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("SnappSdk");

            BearerTokenProvider authProvider = options.TokenProvider is not null
                ? new BearerTokenProvider(options.TokenProvider)
                : new BearerTokenProvider(options.AccessToken ?? throw new InvalidOperationException(
                    "SnappSdkOptions requires either TokenProvider or AccessToken to be set."));

            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient)
            {
                BaseUrl = options.BaseUrl
            };
            return new SnappApiClient(adapter);
        });

        return services;
    }

    /// <summary>
    /// Registers SnappApiClient as a scoped service, allowing the configure delegate
    /// to resolve scoped services (e.g. auth state) via the service provider.
    /// </summary>
    public static IServiceCollection AddSnappSdkScoped(
        this IServiceCollection services,
        Action<IServiceProvider, SnappSdkOptions> configure)
    {
        services.AddScoped(sp =>
        {
            var options = new SnappSdkOptions();
            configure(sp, options);

            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("SnappSdk");
            httpClient.BaseAddress = new Uri(options.BaseUrl);

            BearerTokenProvider authProvider = options.TokenProvider is not null
                ? new BearerTokenProvider(options.TokenProvider)
                : new BearerTokenProvider(options.AccessToken ?? throw new InvalidOperationException(
                    "SnappSdkOptions requires either TokenProvider or AccessToken to be set."));

            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient)
            {
                BaseUrl = options.BaseUrl
            };
            return new SnappApiClient(adapter);
        });

        return services;
    }
}
