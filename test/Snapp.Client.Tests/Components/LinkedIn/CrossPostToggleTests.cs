using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.LinkedIn;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Snapp.Shared.DTOs.LinkedIn;
using Xunit;

namespace Snapp.Client.Tests.Components.LinkedIn;

public class CrossPostToggleTests : TestContext
{
    private readonly MockLinkedInService _linkedInService;

    public CrossPostToggleTests()
    {
        _linkedInService = new MockLinkedInService();
        Services.AddSingleton<ILinkedInService>(_linkedInService);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void NotLinked_RendersNothing()
    {
        _linkedInService.Status = new LinkedInStatusResponse { IsLinked = false };

        var cut = RenderComponent<CrossPostToggle>();

        Assert.DoesNotContain("Also share on LinkedIn", cut.Markup);
    }

    [Fact]
    public void Linked_ShowsToggle()
    {
        _linkedInService.Status = new LinkedInStatusResponse { IsLinked = true };

        var cut = RenderComponent<CrossPostToggle>();

        Assert.Contains("Also share on LinkedIn", cut.Markup);
    }

    [Fact]
    public void Linked_ShowsLinkedInIcon()
    {
        _linkedInService.Status = new LinkedInStatusResponse { IsLinked = true };

        var cut = RenderComponent<CrossPostToggle>();

        // Icon rendered with LinkedIn blue color
        Assert.Contains("0A66C2", cut.Markup);
    }

    [Fact]
    public void ServiceError_RendersNothing()
    {
        _linkedInService.ShouldThrow = true;

        var cut = RenderComponent<CrossPostToggle>();

        Assert.DoesNotContain("Also share on LinkedIn", cut.Markup);
    }
}
