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

public class DirectoryTests : TestContext
{
    private readonly MockNetworkService _networkService;

    public DirectoryTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _networkService = new MockNetworkService();
        Services.AddSingleton<INetworkService>(_networkService);

        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
    }

    [Fact]
    public void Directory_RendersTitle()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Network.Directory>();
        Assert.Contains("Network Directory", cut.Markup);
    }

    [Fact]
    public void Directory_HasCreateButton()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Network.Directory>();
        Assert.Contains("Create Network", cut.Markup);
    }

    [Fact]
    public void Directory_ShowsEmptyState_WhenNoNetworks()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Network.Directory>();
        cut.WaitForState(() => cut.Markup.Contains("No networks found"), TimeSpan.FromSeconds(2));
        Assert.Contains("No networks found", cut.Markup);
    }

    [Fact]
    public void Directory_ListsNetworks()
    {
        _networkService.AllNetworks = new NetworkListResponse
        {
            Networks = new List<NetworkResponse>
            {
                new() { NetworkId = "n1", Name = "Alpha Network", Description = "First", MemberCount = 5 },
                new() { NetworkId = "n2", Name = "Beta Network", Description = "Second", MemberCount = 12 },
            }
        };

        var cut = RenderComponent<Snapp.Client.Pages.Network.Directory>();
        cut.WaitForState(() => cut.Markup.Contains("Alpha Network"), TimeSpan.FromSeconds(2));

        Assert.Contains("Alpha Network", cut.Markup);
        Assert.Contains("Beta Network", cut.Markup);
    }

    [Fact]
    public void Directory_ShowsMemberChip_ForMyNetworks()
    {
        _networkService.AllNetworks = new NetworkListResponse
        {
            Networks = new List<NetworkResponse>
            {
                new() { NetworkId = "n1", Name = "My Net", MemberCount = 3 },
            }
        };
        _networkService.MyNetworks = new NetworkListResponse
        {
            Networks = new List<NetworkResponse>
            {
                new() { NetworkId = "n1", Name = "My Net", UserRole = "member" },
            }
        };

        var cut = RenderComponent<Snapp.Client.Pages.Network.Directory>();
        cut.WaitForState(() => cut.Markup.Contains("Member"), TimeSpan.FromSeconds(2));

        Assert.Contains("Member", cut.Markup);
    }

    [Fact]
    public void Directory_ShowsErrorState_OnFailure()
    {
        _networkService.ShouldThrow = true;

        var cut = RenderComponent<Snapp.Client.Pages.Network.Directory>();
        cut.WaitForState(() => cut.Markup.Contains("Failed to load"), TimeSpan.FromSeconds(2));

        Assert.Contains("Failed to load networks", cut.Markup);
    }
}
