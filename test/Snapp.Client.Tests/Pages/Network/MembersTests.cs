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

public class MembersTests : TestContext
{
    private readonly MockNetworkService _networkService;

    public MembersTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _networkService = new MockNetworkService();
        Services.AddSingleton<INetworkService>(_networkService);
        Services.AddScoped(_ => new NetworkState(JSInterop.JSRuntime));

        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Members_RendersTitle()
    {
        var cut = RenderComponent<Members>(p => p.Add(x => x.NetId, "net1"));
        Assert.Contains("Members", cut.Markup);
    }

    [Fact]
    public void Members_ListsMembersWithRoles()
    {
        _networkService.Members = new MemberListResponse
        {
            Members = new List<MemberResponse>
            {
                new() { UserId = "u1", DisplayName = "Alice", Role = "steward", JoinedAt = DateTime.UtcNow },
                new() { UserId = "u2", DisplayName = "Bob", Role = "member", JoinedAt = DateTime.UtcNow },
            }
        };

        var cut = RenderComponent<Members>(p => p.Add(x => x.NetId, "net1"));
        cut.WaitForState(() => cut.Markup.Contains("Alice"), TimeSpan.FromSeconds(2));

        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
        Assert.Contains("steward", cut.Markup);
    }

    [Fact]
    public void Members_ShowsEmptyState()
    {
        var cut = RenderComponent<Members>(p => p.Add(x => x.NetId, "net1"));
        cut.WaitForState(() => cut.Markup.Contains("No members found"), TimeSpan.FromSeconds(2));

        Assert.Contains("No members found", cut.Markup);
    }

    [Fact]
    public void Members_HasSearchField()
    {
        _networkService.Members = new MemberListResponse
        {
            Members = new List<MemberResponse>
            {
                new() { UserId = "u1", DisplayName = "Alice", Role = "member", JoinedAt = DateTime.UtcNow },
            }
        };

        var cut = RenderComponent<Members>(p => p.Add(x => x.NetId, "net1"));
        Assert.Contains("Search members", cut.Markup);
    }

    [Fact]
    public void Members_ShowsErrorState_OnFailure()
    {
        _networkService.ShouldThrow = true;

        var cut = RenderComponent<Members>(p => p.Add(x => x.NetId, "net1"));
        cut.WaitForState(() => cut.Markup.Contains("Failed to load"), TimeSpan.FromSeconds(2));

        Assert.Contains("Failed to load members", cut.Markup);
    }
}
