using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Snapp.Sdk;
using Snapp.Sdk.Authentication;
using Xunit;

namespace Snapp.Sdk.Tests;

public class SnappApiClientTests
{
    private static SnappApiClient CreateClient()
    {
        var authProvider = new BearerTokenProvider("test-token");
        var adapter = new HttpClientRequestAdapter(authProvider)
        {
            BaseUrl = "http://localhost:8000"
        };
        return new SnappApiClient(adapter);
    }

    [Fact]
    public void Constructor_WithValidAdapter_CreatesClient()
    {
        var client = CreateClient();
        Assert.NotNull(client);
    }

    [Fact]
    public void Api_HasAuthEndpoints()
    {
        var client = CreateClient();
        Assert.NotNull(client.Api.Auth);
    }

    [Fact]
    public void Api_HasUsersEndpoints()
    {
        var client = CreateClient();
        Assert.NotNull(client.Api.Users);
    }

    [Fact]
    public void Api_HasNetworksEndpoints()
    {
        var client = CreateClient();
        Assert.NotNull(client.Api.Networks);
    }

    [Fact]
    public void Api_HasContentEndpoints()
    {
        var client = CreateClient();
        Assert.NotNull(client.Api.Content);
    }

    [Fact]
    public void Api_HasIntelEndpoints()
    {
        var client = CreateClient();
        Assert.NotNull(client.Api.Intel);
    }

    [Fact]
    public void Api_HasTxEndpoints()
    {
        var client = CreateClient();
        Assert.NotNull(client.Api.Tx);
    }

    [Fact]
    public void Api_HasNotifEndpoints()
    {
        var client = CreateClient();
        Assert.NotNull(client.Api.Notif);
    }

    [Fact]
    public void Api_HasLinkedinEndpoints()
    {
        var client = CreateClient();
        Assert.NotNull(client.Api.Linkedin);
    }
}

public class BearerTokenProviderTests
{
    [Fact]
    public void Constructor_WithStaticToken_Succeeds()
    {
        var provider = new BearerTokenProvider("my-token");
        Assert.NotNull(provider);
    }

    [Fact]
    public void Constructor_WithTokenFactory_Succeeds()
    {
        var provider = new BearerTokenProvider(() => Task.FromResult("dynamic-token"));
        Assert.NotNull(provider);
    }

    [Fact]
    public void Constructor_WithEmptyToken_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BearerTokenProvider(""));
    }

    [Fact]
    public void Constructor_WithNullFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BearerTokenProvider((Func<Task<string>>)null!));
    }
}

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSnappSdk_WithAccessToken_RegistersClient()
    {
        var services = new ServiceCollection();

        Snapp.Sdk.Extensions.ServiceCollectionExtensions.AddSnappSdk(services, opts =>
        {
            opts.BaseUrl = "http://localhost:8000";
            opts.AccessToken = "test-token";
        });

        var sp = services.BuildServiceProvider();
        var client = sp.GetService<SnappApiClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddSnappSdk_WithTokenProvider_RegistersClient()
    {
        var services = new ServiceCollection();

        Snapp.Sdk.Extensions.ServiceCollectionExtensions.AddSnappSdk(services, opts =>
        {
            opts.BaseUrl = "http://localhost:8000";
            opts.TokenProvider = () => Task.FromResult("dynamic-token");
        });

        var sp = services.BuildServiceProvider();
        var client = sp.GetService<SnappApiClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddSnappSdk_WithNoToken_ThrowsOnResolve()
    {
        var services = new ServiceCollection();

        Snapp.Sdk.Extensions.ServiceCollectionExtensions.AddSnappSdk(services, opts =>
        {
            opts.BaseUrl = "http://localhost:8000";
        });

        var sp = services.BuildServiceProvider();
        var ex = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<SnappApiClient>());
        Assert.Contains("TokenProvider", ex.Message);
    }
}
