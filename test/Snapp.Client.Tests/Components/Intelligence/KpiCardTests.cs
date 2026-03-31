using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Intelligence;
using Snapp.Shared.DTOs.Intelligence;
using Xunit;

namespace Snapp.Client.Tests.Components.Intelligence;

public class KpiCardTests : TestContext
{
    public KpiCardTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void KpiCard_RendersNameAndValue()
    {
        var kpi = new KpiItem { Name = "Annual Revenue", Value = "1200000", Unit = "USD", Trend = "Up" };

        var cut = RenderComponent<KpiCard>(p => p.Add(c => c.Kpi, kpi));

        Assert.Contains("Annual Revenue", cut.Markup);
        Assert.Contains("$1200000", cut.Markup);
    }

    [Fact]
    public void KpiCard_PercentageUnit_FormatsCorrectly()
    {
        var kpi = new KpiItem { Name = "Overhead Ratio", Value = "65.2", Unit = "%", Trend = "Flat" };

        var cut = RenderComponent<KpiCard>(p => p.Add(c => c.Kpi, kpi));

        Assert.Contains("65.2%", cut.Markup);
    }

    [Fact]
    public void KpiCard_WithPercentile_ShowsProgressBar()
    {
        var kpi = new KpiItem { Name = "Revenue", Value = "1M", Percentile = 75m, Trend = "Up" };

        var cut = RenderComponent<KpiCard>(p => p.Add(c => c.Kpi, kpi));

        var progress = cut.FindComponent<MudProgressLinear>();
        Assert.NotNull(progress);
        Assert.Contains("75th percentile", cut.Markup);
    }

    [Fact]
    public void KpiCard_WithoutPercentile_NoProgressBar()
    {
        var kpi = new KpiItem { Name = "Revenue", Value = "1M", Percentile = null, Trend = "Flat" };

        var cut = RenderComponent<KpiCard>(p => p.Add(c => c.Kpi, kpi));

        var progressBars = cut.FindComponents<MudProgressLinear>();
        Assert.Empty(progressBars);
    }

    [Fact]
    public void KpiCard_TrendUp_ShowsGreenArrow()
    {
        var kpi = new KpiItem { Name = "Test", Value = "100", Trend = "Up" };

        var cut = RenderComponent<KpiCard>(p => p.Add(c => c.Kpi, kpi));

        var icon = cut.FindComponent<MudIcon>();
        Assert.Equal(Color.Success, icon.Instance.Color);
    }

    [Fact]
    public void KpiCard_TrendDown_ShowsRedArrow()
    {
        var kpi = new KpiItem { Name = "Test", Value = "100", Trend = "Down" };

        var cut = RenderComponent<KpiCard>(p => p.Add(c => c.Kpi, kpi));

        var icon = cut.FindComponent<MudIcon>();
        Assert.Equal(Color.Error, icon.Instance.Color);
    }
}
