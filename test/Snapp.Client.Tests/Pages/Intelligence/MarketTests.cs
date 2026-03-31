using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Intelligence;
using Snapp.Client.Services;
using Xunit;

namespace Snapp.Client.Tests.Pages.Intelligence;

public class MarketTests : TestContext
{
    private readonly FakeIntelligenceService _service = new();

    public MarketTests()
    {
        Services.AddSingleton<IIntelligenceService>(_service);
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Market_InitialRender_ShowsGeographySelector()
    {
        var cut = RenderComponent<Market>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Market Intelligence", cut.Markup);
            Assert.Contains("Select Geography", cut.Markup);
        });
    }

    [Fact]
    public void Market_ShowsViewMarketButton()
    {
        var cut = RenderComponent<Market>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("View Market", cut.Markup);
        });
    }

    [Fact]
    public void Market_ShowsCompareField()
    {
        var cut = RenderComponent<Market>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Compare With", cut.Markup);
        });
    }

    [Fact]
    public void Market_ShowsBackToDashboard()
    {
        var cut = RenderComponent<Market>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Back to Dashboard", cut.Markup);
        });
    }

    private class FakeAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new System.Security.Claims.ClaimsIdentity("test");
            var user = new System.Security.Claims.ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(user));
        }
    }
}
