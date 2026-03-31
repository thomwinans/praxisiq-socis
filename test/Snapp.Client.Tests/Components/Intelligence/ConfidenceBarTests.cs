using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Intelligence;
using Xunit;

namespace Snapp.Client.Tests.Components.Intelligence;

public class ConfidenceBarTests : TestContext
{
    public ConfidenceBarTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ConfidenceBar_RendersScoreAndTier()
    {
        var cut = RenderComponent<ConfidenceBar>(p => p.Add(c => c.Score, 72m));

        Assert.Contains("72%", cut.Markup);
        Assert.Contains("Good", cut.Markup);
    }

    [Theory]
    [InlineData(10, "Low")]
    [InlineData(45, "Fair")]
    [InlineData(70, "Good")]
    [InlineData(90, "Excellent")]
    public void ConfidenceBar_ShowsCorrectTierLabel(int score, string expectedTier)
    {
        var cut = RenderComponent<ConfidenceBar>(p => p.Add(c => c.Score, (decimal)score));

        Assert.Contains(expectedTier, cut.Markup);
    }

    [Fact]
    public void ConfidenceBar_RendersProgressBar()
    {
        var cut = RenderComponent<ConfidenceBar>(p => p.Add(c => c.Score, 50m));

        var progress = cut.FindComponent<MudProgressLinear>();
        Assert.NotNull(progress);
    }

    [Fact]
    public void ConfidenceBar_LowScore_ShowsContributeMessage()
    {
        var cut = RenderComponent<ConfidenceBar>(p => p.Add(c => c.Score, 20m));

        Assert.Contains("Contribute data to get started", cut.Markup);
    }

    [Fact]
    public void ConfidenceBar_HighScore_ShowsHighConfidence()
    {
        var cut = RenderComponent<ConfidenceBar>(p => p.Add(c => c.Score, 90m));

        Assert.Contains("High confidence in your data", cut.Markup);
    }
}
