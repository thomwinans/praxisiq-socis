using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Transaction;
using Xunit;

namespace Snapp.Client.Tests.Components.Transaction;

public class ReputationBadgeTests : TestContext
{
    public ReputationBadgeTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void ReputationBadge_RendersOverallScore()
    {
        var cut = RenderComponent<ReputationBadge>(parameters => parameters
            .Add(p => p.OverallScore, 85m)
            .Add(p => p.ReferralScore, 90m)
            .Add(p => p.ContributionScore, 80m)
            .Add(p => p.AttestationScore, 85m));

        Assert.Contains("85", cut.Markup);
    }

    [Fact]
    public void ReputationBadge_UsesSuccessColor_WhenScoreHigh()
    {
        var cut = RenderComponent<ReputationBadge>(parameters => parameters
            .Add(p => p.OverallScore, 75m));

        var chip = cut.FindComponent<MudChip<string>>();
        Assert.Equal(Color.Success, chip.Instance.Color);
    }

    [Fact]
    public void ReputationBadge_UsesWarningColor_WhenScoreMedium()
    {
        var cut = RenderComponent<ReputationBadge>(parameters => parameters
            .Add(p => p.OverallScore, 50m));

        var chip = cut.FindComponent<MudChip<string>>();
        Assert.Equal(Color.Warning, chip.Instance.Color);
    }

    [Fact]
    public void ReputationBadge_UsesErrorColor_WhenScoreLow()
    {
        var cut = RenderComponent<ReputationBadge>(parameters => parameters
            .Add(p => p.OverallScore, 20m));

        var chip = cut.FindComponent<MudChip<string>>();
        Assert.Equal(Color.Error, chip.Instance.Color);
    }

    [Fact]
    public void ReputationBadge_RendersAsChip()
    {
        var cut = RenderComponent<ReputationBadge>(parameters => parameters
            .Add(p => p.OverallScore, 60m));

        var chip = cut.FindComponent<MudChip<string>>();
        Assert.NotNull(chip);
    }

    [Fact]
    public void ReputationBadge_HasTooltip()
    {
        var cut = RenderComponent<ReputationBadge>(parameters => parameters
            .Add(p => p.OverallScore, 60m)
            .Add(p => p.ReferralScore, 70m)
            .Add(p => p.ContributionScore, 50m)
            .Add(p => p.AttestationScore, 60m));

        var tooltip = cut.FindComponent<MudTooltip>();
        Assert.NotNull(tooltip);
    }
}
