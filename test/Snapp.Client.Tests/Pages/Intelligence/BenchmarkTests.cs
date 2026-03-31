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

public class BenchmarkTests : TestContext
{
    private readonly FakeIntelligenceService _service = new();

    public BenchmarkTests()
    {
        Services.AddSingleton<IIntelligenceService>(_service);
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Benchmark_InitialState_ShowsCohortSelectors()
    {
        var cut = RenderComponent<Benchmark>();

        Assert.Contains("Specialty", cut.Markup);
        Assert.Contains("Geography", cut.Markup);
        Assert.Contains("Size Band", cut.Markup);
        Assert.Contains("Compare", cut.Markup);
    }

    [Fact]
    public void Benchmark_InitialState_ShowsEmptyPrompt()
    {
        var cut = RenderComponent<Benchmark>();

        Assert.Contains("Select cohort filters", cut.Markup);
    }

    [Fact]
    public void Benchmark_Error_ShowsErrorAlert()
    {
        _service.ThrowOnBenchmarks = true;
        var cut = RenderComponent<Benchmark>();

        cut.Find("button.mud-button-filled").Click();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
            Assert.Contains("Failed to load benchmark data", cut.Markup);
        });
    }

    [Fact]
    public void Benchmark_NullResponse_ShowsError()
    {
        _service.BenchmarkResult = null;
        var cut = RenderComponent<Benchmark>();

        cut.Find("button.mud-button-filled").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed to load benchmark data", cut.Markup);
        });
    }

    [Fact]
    public void Benchmark_SmallCohort_ShowsNotEnoughDataAlert()
    {
        _service.BenchmarkResult = new BenchmarkResponse
        {
            CohortSize = 3,
            Metrics = new List<BenchmarkMetric>
            {
                new() { Name = "Revenue", P25 = 500_000, P50 = 750_000, P75 = 1_000_000 },
            },
        };

        var cut = RenderComponent<Benchmark>();
        cut.Find("button.mud-button-filled").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Not enough data in this cohort yet", cut.Markup);
            Assert.Contains("3 practices", cut.Markup);
        });
    }

    [Fact]
    public void Benchmark_WithMetrics_ShowsPeerCount()
    {
        _service.BenchmarkResult = CreateSampleResponse(20);
        var cut = RenderComponent<Benchmark>();

        cut.Find("button.mud-button-filled").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Comparing against 20 peers", cut.Markup);
        });
    }

    [Fact]
    public void Benchmark_WithMetrics_ShowsTable()
    {
        _service.BenchmarkResult = CreateSampleResponse(15);
        var cut = RenderComponent<Benchmark>();

        cut.Find("button.mud-button-filled").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Annual Revenue", cut.Markup);
            Assert.Contains("Overhead Ratio", cut.Markup);
            Assert.Contains("P25", cut.Markup);
            Assert.Contains("P50", cut.Markup);
            Assert.Contains("P75", cut.Markup);
        });
    }

    [Fact]
    public void Benchmark_WithUserPercentile_ShowsPercentileChip()
    {
        _service.BenchmarkResult = new BenchmarkResponse
        {
            CohortSize = 25,
            Metrics = new List<BenchmarkMetric>
            {
                new() { Name = "Revenue", P25 = 500_000, P50 = 750_000, P75 = 1_000_000, UserValue = 900_000, UserPercentile = 68 },
            },
        };

        var cut = RenderComponent<Benchmark>();
        cut.Find("button.mud-button-filled").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("68th", cut.Markup);
        });
    }

    [Fact]
    public void Benchmark_NoUserValue_ShowsDashes()
    {
        _service.BenchmarkResult = new BenchmarkResponse
        {
            CohortSize = 10,
            Metrics = new List<BenchmarkMetric>
            {
                new() { Name = "Revenue", P25 = 500_000, P50 = 750_000, P75 = 1_000_000 },
            },
        };

        var cut = RenderComponent<Benchmark>();
        cut.Find("button.mud-button-filled").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("--", cut.Markup);
        });
    }

    [Fact]
    public void Benchmark_HasRetryButton_OnError()
    {
        _service.ThrowOnBenchmarks = true;
        var cut = RenderComponent<Benchmark>();

        cut.Find("button.mud-button-filled").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Retry", cut.Markup);
        });
    }

    private static BenchmarkResponse CreateSampleResponse(int cohortSize) => new()
    {
        CohortSize = cohortSize,
        Metrics = new List<BenchmarkMetric>
        {
            new() { Name = "Annual Revenue", P25 = 500_000, P50 = 750_000, P75 = 1_000_000, UserValue = 850_000, UserPercentile = 62 },
            new() { Name = "Overhead Ratio", P25 = 55, P50 = 62, P75 = 70, UserValue = 58, UserPercentile = 35 },
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
