using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Network;
using Snapp.Client.Services;
using Snapp.Client.State;
using Snapp.Client.Tests.Mocks;
using Snapp.Shared.DTOs.Network;
using Xunit;

namespace Snapp.Client.Tests.Pages.Network;

public class HomeTests : TestContext
{
    private readonly MockNetworkService _networkService;

    public HomeTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _networkService = new MockNetworkService();
        Services.AddSingleton<INetworkService>(_networkService);
        Services.AddScoped(_ => new NetworkState(JSInterop.JSRuntime));

        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Home_ShowsNetworkDashboard()
    {
        _networkService.Network = new NetworkResponse
        {
            NetworkId = "net1",
            Name = "Test Network",
            Description = "A great network",
            MemberCount = 42,
            CreatedAt = new DateTime(2025, 6, 1),
        };

        var cut = RenderComponent<Home>(p => p.Add(x => x.NetId, "net1"));
        cut.WaitForState(() => cut.Markup.Contains("Test Network"), TimeSpan.FromSeconds(2));

        Assert.Contains("Test Network", cut.Markup);
        Assert.Contains("42", cut.Markup);
        Assert.Contains("Dashboard", cut.Markup);
    }

    [Fact]
    public void Home_ShowsQuickLinks()
    {
        _networkService.Network = new NetworkResponse
        {
            NetworkId = "net1",
            Name = "Test Network",
            MemberCount = 5,
        };

        var cut = RenderComponent<Home>(p => p.Add(x => x.NetId, "net1"));
        cut.WaitForState(() => cut.Markup.Contains("Quick Links"), TimeSpan.FromSeconds(2));

        Assert.Contains("Feed", cut.Markup);
        Assert.Contains("Discussions", cut.Markup);
        Assert.Contains("Members", cut.Markup);
    }

    [Fact]
    public void Home_ShowsNotFound_ForInvalidNetwork()
    {
        _networkService.Network = null;

        var cut = RenderComponent<Home>(p => p.Add(x => x.NetId, "bad-id"));
        cut.WaitForState(() => cut.Markup.Contains("Network not found"), TimeSpan.FromSeconds(2));

        Assert.Contains("Network not found", cut.Markup);
    }

    [Fact]
    public void Home_ShowsDescription()
    {
        _networkService.Network = new NetworkResponse
        {
            NetworkId = "net1",
            Name = "Test",
            Description = "This is the network description",
            MemberCount = 1,
        };

        var cut = RenderComponent<Home>(p => p.Add(x => x.NetId, "net1"));
        cut.WaitForState(() => cut.Markup.Contains("About"), TimeSpan.FromSeconds(2));

        Assert.Contains("This is the network description", cut.Markup);
    }
}
