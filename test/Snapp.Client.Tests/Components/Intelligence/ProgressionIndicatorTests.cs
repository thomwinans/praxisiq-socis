using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Intelligence;
using Xunit;

namespace Snapp.Client.Tests.Components.Intelligence;

public class ProgressionIndicatorTests : TestContext
{
    public ProgressionIndicatorTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void ProgressionIndicator_ShowsUnlockCount()
    {
        var cut = RenderComponent<ProgressionIndicator>(p =>
        {
            p.Add(c => c.TotalUnlocks, 5);
            p.Add(c => c.TotalAnswered, 8);
            p.Add(c => c.CurrentStreak, 3);
        });

        Assert.Contains("5 insights unlocked", cut.Markup);
    }

    [Fact]
    public void ProgressionIndicator_SingleInsight_NoPlural()
    {
        var cut = RenderComponent<ProgressionIndicator>(p =>
        {
            p.Add(c => c.TotalUnlocks, 1);
            p.Add(c => c.TotalAnswered, 1);
            p.Add(c => c.CurrentStreak, 1);
        });

        Assert.Contains("1 insight unlocked", cut.Markup);
    }

    [Fact]
    public void ProgressionIndicator_ShowsStreak()
    {
        var cut = RenderComponent<ProgressionIndicator>(p =>
        {
            p.Add(c => c.TotalUnlocks, 5);
            p.Add(c => c.TotalAnswered, 8);
            p.Add(c => c.CurrentStreak, 3);
        });

        Assert.Contains("3 day streak", cut.Markup);
    }

    [Fact]
    public void ProgressionIndicator_ZeroStreak_HidesStreakChip()
    {
        var cut = RenderComponent<ProgressionIndicator>(p =>
        {
            p.Add(c => c.TotalUnlocks, 5);
            p.Add(c => c.TotalAnswered, 8);
            p.Add(c => c.CurrentStreak, 0);
        });

        Assert.DoesNotContain("day streak", cut.Markup);
    }

    [Fact]
    public void ProgressionIndicator_RendersChips()
    {
        var cut = RenderComponent<ProgressionIndicator>(p =>
        {
            p.Add(c => c.TotalUnlocks, 3);
            p.Add(c => c.TotalAnswered, 5);
            p.Add(c => c.CurrentStreak, 2);
        });

        var chips = cut.FindComponents<MudChip<string>>();
        Assert.Equal(2, chips.Count); // unlocks chip + streak chip
    }
}
