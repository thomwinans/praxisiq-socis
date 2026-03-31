using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Snapp.Shared.DTOs.Transaction;
using Snapp.Shared.Enums;
using Xunit;

namespace Snapp.Client.Tests.Pages.Referrals;

public class ListTests : TestContext
{
    private readonly MockReferralService _referralService;
    private readonly MockNetworkService _networkService;

    public ListTests()
    {
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _referralService = new MockReferralService();
        _networkService = new MockNetworkService();
        Services.AddSingleton<IReferralService>(_referralService);
        Services.AddSingleton<INetworkService>(_networkService);
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());

        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
    }

    [Fact]
    public void List_RendersTitle()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Referrals.List>();
        Assert.Contains("Referrals", cut.Markup);
    }

    [Fact]
    public void List_HasNewReferralButton()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Referrals.List>();
        Assert.Contains("New Referral", cut.Markup);
    }

    [Fact]
    public void List_ShowsSentAndReceivedTabs()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Referrals.List>();
        cut.WaitForState(() => !cut.Markup.Contains("mud-progress-circular"), TimeSpan.FromSeconds(2));

        Assert.Contains("Sent", cut.Markup);
        Assert.Contains("Received", cut.Markup);
    }

    [Fact]
    public void List_ShowsEmptyState_WhenNoReferrals()
    {
        var cut = RenderComponent<Snapp.Client.Pages.Referrals.List>();
        cut.WaitForState(() => cut.Markup.Contains("No sent referrals"), TimeSpan.FromSeconds(2));

        Assert.Contains("No sent referrals yet", cut.Markup);
    }

    [Fact]
    public void List_ShowsSentReferrals()
    {
        _referralService.SentReferrals = new ReferralListResponse
        {
            Referrals = new List<ReferralResponse>
            {
                new()
                {
                    ReferralId = "ref1",
                    SenderUserId = "user1",
                    SenderDisplayName = "Alice",
                    ReceiverUserId = "user2",
                    ReceiverDisplayName = "Bob",
                    NetworkId = "net1",
                    Specialty = "Endodontics",
                    Status = ReferralStatus.Created,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        var cut = RenderComponent<Snapp.Client.Pages.Referrals.List>();
        cut.WaitForState(() => cut.Markup.Contains("Alice"), TimeSpan.FromSeconds(2));

        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
        Assert.Contains("Endodontics", cut.Markup);
    }

    [Fact]
    public void List_ShowsErrorState_OnFailure()
    {
        _referralService.ShouldThrow = true;

        var cut = RenderComponent<Snapp.Client.Pages.Referrals.List>();
        cut.WaitForState(() => cut.Markup.Contains("Failed to load"), TimeSpan.FromSeconds(2));

        Assert.Contains("Failed to load referrals", cut.Markup);
    }

    private class FakeAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var claims = new[] { new System.Security.Claims.Claim("sub", "user1") };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "test");
            var user = new System.Security.Claims.ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(user));
        }
    }
}
