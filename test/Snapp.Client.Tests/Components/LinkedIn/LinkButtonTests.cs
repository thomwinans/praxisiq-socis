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

public class LinkButtonTests : TestContext
{
    private readonly MockLinkedInService _linkedInService;

    public LinkButtonTests()
    {
        _linkedInService = new MockLinkedInService();
        Services.AddSingleton<ILinkedInService>(_linkedInService);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void NotLinked_ShowsConnectButton()
    {
        _linkedInService.Status = new LinkedInStatusResponse { IsLinked = false };

        var cut = RenderComponent<LinkButton>();

        Assert.Contains("Connect LinkedIn", cut.Markup);
    }

    [Fact]
    public void Linked_ShowsConnectedChipAndUnlink()
    {
        _linkedInService.Status = new LinkedInStatusResponse { IsLinked = true, LinkedInName = "Test User" };

        var cut = RenderComponent<LinkButton>();

        Assert.Contains("LinkedIn Connected", cut.Markup);
        Assert.Contains("Unlink", cut.Markup);
    }

    [Fact]
    public void Unlink_CallsServiceAndShowsConnectButton()
    {
        _linkedInService.Status = new LinkedInStatusResponse { IsLinked = true };

        var cut = RenderComponent<LinkButton>();
        Assert.Contains("LinkedIn Connected", cut.Markup);

        var unlinkButton = cut.FindAll("button").First(b => b.TextContent.Contains("Unlink"));
        unlinkButton.Click();

        Assert.Equal(1, _linkedInService.UnlinkCallCount);
        Assert.Contains("Connect LinkedIn", cut.Markup);
    }

    [Fact]
    public void ServiceError_ShowsConnectButton()
    {
        _linkedInService.ShouldThrow = true;

        var cut = RenderComponent<LinkButton>();

        Assert.Contains("Connect LinkedIn", cut.Markup);
    }

    [Fact]
    public void Linked_IsLinkedPropertyReturnsTrue()
    {
        _linkedInService.Status = new LinkedInStatusResponse { IsLinked = true };

        var cut = RenderComponent<LinkButton>();

        Assert.True(cut.Instance.IsLinked);
    }

    [Fact]
    public void NotLinked_IsLinkedPropertyReturnsFalse()
    {
        _linkedInService.Status = new LinkedInStatusResponse { IsLinked = false };

        var cut = RenderComponent<LinkButton>();

        Assert.False(cut.Instance.IsLinked);
    }
}
