using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Intelligence;
using Snapp.Client.Services;
using Snapp.Shared.DTOs.Intelligence;
using Xunit;

namespace Snapp.Client.Tests.Pages.Intelligence;

public class ValuationTests : TestContext
{
    private readonly FakeIntelligenceService _service = new();

    public ValuationTests()
    {
        Services.AddSingleton<IIntelligenceService>(_service);
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Valuation_NoData_ShowsContributePrompt()
    {
        _service.ValuationResult = null;
        var cut = RenderComponent<Valuation>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No valuation available yet", cut.Markup);
            Assert.Contains("Get Started", cut.Markup);
        });
    }

    [Fact]
    public void Valuation_Error_ShowsErrorAlert()
    {
        _service.ThrowOnValuation = true;
        var cut = RenderComponent<Valuation>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed to load valuation data", cut.Markup);
        });
    }

    [Fact]
    public void Valuation_WithData_ShowsThreeCases()
    {
        _service.ValuationResult = CreateValuation();
        var cut = RenderComponent<Valuation>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Downside", cut.Markup);
            Assert.Contains("Base Case", cut.Markup);
            Assert.Contains("Upside", cut.Markup);
            Assert.Contains("$1.2M", cut.Markup);
        });
    }

    [Fact]
    public void Valuation_ShowsConfidenceBadge()
    {
        _service.ValuationResult = CreateValuation();
        var cut = RenderComponent<Valuation>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("72% Confidence", cut.Markup);
        });
    }

    [Fact]
    public void Valuation_ShowsDrivers()
    {
        _service.ValuationResult = CreateValuation();
        var cut = RenderComponent<Valuation>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Valuation Drivers", cut.Markup);
            Assert.Contains("Revenue Growth", cut.Markup);
            Assert.Contains("Strong year-over-year growth supports higher valuation", cut.Markup);
        });
    }

    [Fact]
    public void Valuation_ShowsScenarioControls()
    {
        _service.ValuationResult = CreateValuation();
        var cut = RenderComponent<Valuation>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Scenario Modeling", cut.Markup);
            Assert.Contains("Recalculate", cut.Markup);
        });
    }

    [Fact]
    public void Valuation_WithHistory_ShowsChart()
    {
        _service.ValuationResult = CreateValuation();
        var cut = RenderComponent<Valuation>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Valuation History", cut.Markup);
            var chart = cut.FindComponent<MudChart>();
            Assert.NotNull(chart);
        });
    }

    [Fact]
    public void Valuation_NoHistory_ShowsEmptyMessage()
    {
        _service.ValuationResult = new ValuationResponse
        {
            Downside = 800_000,
            Base = 1_200_000,
            Upside = 1_600_000,
            ConfidenceScore = 72,
            Drivers = new(),
            History = new(),
        };

        var cut = RenderComponent<Valuation>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("History will appear after multiple valuation computations", cut.Markup);
        });
    }

    private static ValuationResponse CreateValuation() => new()
    {
        Downside = 800_000,
        Base = 1_200_000,
        Upside = 1_600_000,
        ConfidenceScore = 72,
        Drivers = new List<ValuationDriver>
        {
            new() { Name = "RevenueGrowth", Impact = "Strong year-over-year growth supports higher valuation", Direction = "positive" },
            new() { Name = "OwnerRisk", Impact = "Elevated owner production dependency suppresses multiple", Direction = "negative" },
            new() { Name = "MarketPosition", Impact = "Moderate market conditions", Direction = "neutral" },
        },
        History = new List<ValuationSnapshot>
        {
            new() { Date = new DateTime(2025, 10, 1), Base = 1_000_000, ConfidenceScore = 60 },
            new() { Date = new DateTime(2025, 12, 1), Base = 1_100_000, ConfidenceScore = 65 },
            new() { Date = new DateTime(2026, 2, 1), Base = 1_200_000, ConfidenceScore = 72 },
        },
    };

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
