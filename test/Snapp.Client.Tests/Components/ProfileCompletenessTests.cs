using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace Snapp.Client.Tests.Components;

public class ProfileCompletenessTests : TestContext
{
    public ProfileCompletenessTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void ProfileCompleteness_RendersProgressBar()
    {
        var cut = RenderComponent<Snapp.Client.Components.ProfileCompleteness>(
            parameters => parameters.Add(p => p.Value, 50m));

        var progress = cut.FindComponent<MudProgressLinear>();
        Assert.NotNull(progress);
    }

    [Theory]
    [InlineData(20, Color.Error)]
    [InlineData(50, Color.Warning)]
    [InlineData(80, Color.Success)]
    [InlineData(100, Color.Success)]
    public void ProfileCompleteness_UsesCorrectColor(int value, Color expectedColor)
    {
        var cut = RenderComponent<Snapp.Client.Components.ProfileCompleteness>(
            parameters => parameters.Add(p => p.Value, (decimal)value));

        var progress = cut.FindComponent<MudProgressLinear>();
        Assert.Equal(expectedColor, progress.Instance.Color);
    }

    [Fact]
    public void ProfileCompleteness_ShowsPercentage()
    {
        var cut = RenderComponent<Snapp.Client.Components.ProfileCompleteness>(
            parameters => parameters.Add(p => p.Value, 75m));

        Assert.Contains("75%", cut.Markup);
    }
}
