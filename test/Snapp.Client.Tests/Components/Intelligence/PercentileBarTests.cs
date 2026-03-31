using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Intelligence;
using Xunit;

namespace Snapp.Client.Tests.Components.Intelligence;

public class PercentileBarTests : TestContext
{
    public PercentileBarTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void PercentileBar_RendersProgressBar()
    {
        var cut = RenderComponent<PercentileBar>(parameters => parameters
            .Add(p => p.P25, 500)
            .Add(p => p.P50, 750)
            .Add(p => p.P75, 1000));

        Assert.Contains("mud-progress-linear", cut.Markup);
    }

    [Fact]
    public void PercentileBar_ShowsPercentileMarkers()
    {
        var cut = RenderComponent<PercentileBar>(parameters => parameters
            .Add(p => p.P25, 500)
            .Add(p => p.P50, 750)
            .Add(p => p.P75, 1000));

        // Three marker divs at 25%, 50%, 75%
        Assert.Contains("left: 25%", cut.Markup);
        Assert.Contains("left: 50%", cut.Markup);
        Assert.Contains("left: 75%", cut.Markup);
    }

    [Fact]
    public void PercentileBar_WithUserPercentile_ShowsDot()
    {
        var cut = RenderComponent<PercentileBar>(parameters => parameters
            .Add(p => p.P25, 500)
            .Add(p => p.P50, 750)
            .Add(p => p.P75, 1000)
            .Add(p => p.UserValue, 850m)
            .Add(p => p.UserPercentile, 65m));

        Assert.Contains("border-radius: 50%", cut.Markup);
        Assert.Contains("65%", cut.Markup);
    }

    [Fact]
    public void PercentileBar_NoUserPercentile_NoDot()
    {
        var cut = RenderComponent<PercentileBar>(parameters => parameters
            .Add(p => p.P25, 500)
            .Add(p => p.P50, 750)
            .Add(p => p.P75, 1000));

        Assert.DoesNotContain("border-radius: 50%", cut.Markup);
    }

    [Fact]
    public void PercentileBar_LowPercentile_ErrorColor()
    {
        var cut = RenderComponent<PercentileBar>(parameters => parameters
            .Add(p => p.P25, 500)
            .Add(p => p.P50, 750)
            .Add(p => p.P75, 1000)
            .Add(p => p.UserValue, 300m)
            .Add(p => p.UserPercentile, 15m));

        Assert.Contains("--mud-palette-error", cut.Markup);
    }

    [Fact]
    public void PercentileBar_HighPercentile_SuccessColor()
    {
        var cut = RenderComponent<PercentileBar>(parameters => parameters
            .Add(p => p.P25, 500)
            .Add(p => p.P50, 750)
            .Add(p => p.P75, 1000)
            .Add(p => p.UserValue, 900m)
            .Add(p => p.UserPercentile, 70m));

        Assert.Contains("--mud-palette-success", cut.Markup);
    }
}
