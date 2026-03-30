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

public class SettingsTests : TestContext
{
    private readonly MockNetworkService _networkService;
    private readonly NetworkState _networkState;

    public SettingsTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _networkService = new MockNetworkService();
        _networkState = new NetworkState(JSInterop.JSRuntime);

        Services.AddSingleton<INetworkService>(_networkService);
        Services.AddScoped(_ => _networkState);

        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Settings_DeniesAccessForNonSteward()
    {
        var cut = RenderComponent<Settings>(p => p.Add(x => x.NetId, "net1"));
        Assert.Contains("do not have permission", cut.Markup);
    }

    [Fact]
    public async Task Settings_RendersTabsForSteward()
    {
        await _networkState.SetCurrentNetworkAsync("net1", "Test", "steward");

        _networkService.Network = new NetworkResponse
        {
            NetworkId = "net1",
            Name = "Test Network",
            Description = "Desc",
            Charter = "Charter text",
        };

        var cut = RenderComponent<Settings>(p => p.Add(x => x.NetId, "net1"));
        cut.WaitForState(() => cut.Markup.Contains("General"), TimeSpan.FromSeconds(2));

        Assert.Contains("General", cut.Markup);
        Assert.Contains("Roles", cut.Markup);
        Assert.Contains("Applications", cut.Markup);
    }

    [Fact]
    public async Task Settings_HasApplicationsTab()
    {
        await _networkState.SetCurrentNetworkAsync("net1", "Test", "steward");

        _networkService.Network = new NetworkResponse
        {
            NetworkId = "net1",
            Name = "Test Network",
        };

        var cut = RenderComponent<Settings>(p => p.Add(x => x.NetId, "net1"));
        cut.WaitForState(() => cut.Markup.Contains("Applications"), TimeSpan.FromSeconds(2));

        Assert.Contains("Applications", cut.Markup);
    }

    [Fact]
    public async Task Settings_HasRolesTab()
    {
        await _networkState.SetCurrentNetworkAsync("net1", "Test", "steward");

        _networkService.Network = new NetworkResponse
        {
            NetworkId = "net1",
            Name = "Test Network",
        };

        var cut = RenderComponent<Settings>(p => p.Add(x => x.NetId, "net1"));
        cut.WaitForState(() => cut.Markup.Contains("Roles"), TimeSpan.FromSeconds(2));

        Assert.Contains("Roles", cut.Markup);
    }

    [Fact]
    public async Task Settings_ShowsSaveButton()
    {
        await _networkState.SetCurrentNetworkAsync("net1", "Test", "steward");

        _networkService.Network = new NetworkResponse
        {
            NetworkId = "net1",
            Name = "Test Network",
        };

        var cut = RenderComponent<Settings>(p => p.Add(x => x.NetId, "net1"));
        cut.WaitForState(() => cut.Markup.Contains("Save Changes"), TimeSpan.FromSeconds(2));

        Assert.Contains("Save Changes", cut.Markup);
    }
}
