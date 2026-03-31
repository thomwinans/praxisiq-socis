using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Intelligence;
using Snapp.Client.Services;
using Snapp.Shared.DTOs.Common;
using Xunit;

namespace Snapp.Client.Tests.Pages.Intelligence;

public class ContributeTests : TestContext
{
    private readonly FakeIntelligenceService _service = new();

    public ContributeTests()
    {
        Services.AddSingleton<IIntelligenceService>(_service);
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());
        JSInterop.Mode = JSRuntimeMode.Loose;

        _service.ConfigResult = CreateTestConfig();
    }

    [Fact]
    public void Contribute_RendersTitle()
    {
        var cut = RenderComponent<Contribute>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Contribute Practice Data", cut.Markup);
        });
    }

    [Fact]
    public void Contribute_ShowsTabs()
    {
        var cut = RenderComponent<Contribute>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Financial Data", cut.Markup);
        });
    }

    [Fact]
    public void Contribute_NoConfig_ShowsError()
    {
        _service.ConfigResult = null;
        var cut = RenderComponent<Contribute>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed to load configuration", cut.Markup);
        });
    }

    [Fact]
    public void Contribute_ShowsBackLink()
    {
        var cut = RenderComponent<Contribute>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Back to Dashboard", cut.Markup);
        });
    }

    [Fact]
    public void Contribute_RendersDynamicFormFields()
    {
        var cut = RenderComponent<Contribute>();

        cut.WaitForAssertion(() =>
        {
            // Should show KPI fields from the financial category (first tab)
            Assert.Contains("Annual Revenue", cut.Markup);
        });
    }

    private static VerticalConfigResponse CreateTestConfig() => new()
    {
        Vertical = "dental",
        DisplayName = "Dental Practice",
        Dimensions = new()
        {
            new()
            {
                Name = "FinancialHealth",
                DisplayName = "Financial Health",
                Weight = 0.25m,
                Kpis = new()
                {
                    new() { Name = "AnnualRevenue", DisplayName = "Annual Revenue", Unit = "USD", Category = "financial" },
                    new() { Name = "OverheadRatio", DisplayName = "Overhead Ratio", Unit = "%", Category = "financial" },
                },
            },
            new()
            {
                Name = "Operations",
                DisplayName = "Operations",
                Weight = 0.20m,
                Kpis = new()
                {
                    new() { Name = "NewPatientsPerMonth", DisplayName = "New Patients / Month", Unit = "count", Category = "operations" },
                },
            },
        },
        ContributionCategories = new()
        {
            new() { Category = "financial", Dimension = "FinancialHealth", ConfidenceWeight = 0.15m, DisplayName = "Financial Data" },
            new() { Category = "operations", Dimension = "Operations", ConfidenceWeight = 0.12m, DisplayName = "Operational Data" },
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
