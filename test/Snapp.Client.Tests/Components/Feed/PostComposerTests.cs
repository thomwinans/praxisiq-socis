using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Feed;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Snapp.Shared.DTOs.LinkedIn;
using Snapp.Shared.Enums;
using Xunit;

namespace Snapp.Client.Tests.Components.Feed;

public class PostComposerTests : TestContext
{
    private readonly MockLinkedInService _linkedInService;

    public PostComposerTests()
    {
        _linkedInService = new MockLinkedInService();
        Services.AddSingleton<ILinkedInService>(_linkedInService);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void PostComposer_RendersTextFieldAndButton()
    {
        var cut = RenderComponent<PostComposer>(p => p
            .Add(x => x.NetworkName, "Test Network")
            .Add(x => x.NetworkId, "net1"));

        var textField = cut.FindComponent<MudTextField<string>>();
        Assert.NotNull(textField);
        Assert.Contains("Post", cut.Markup);
    }

    [Fact]
    public void PostComposer_ShowsNetworkName()
    {
        var cut = RenderComponent<PostComposer>(p => p
            .Add(x => x.NetworkName, "My Guild")
            .Add(x => x.NetworkId, "net1"));

        Assert.Contains("Posting to:", cut.Markup);
        Assert.Contains("My Guild", cut.Markup);
    }

    [Fact]
    public void PostComposer_SubmitButton_DisabledWhenEmpty()
    {
        var cut = RenderComponent<PostComposer>(p => p
            .Add(x => x.NetworkName, "Test")
            .Add(x => x.NetworkId, "net1"));

        var buttons = cut.FindComponents<MudButton>();
        var submitButton = buttons.First(b => b.Markup.Contains("Post"));
        Assert.True(submitButton.Instance.Disabled);
    }

    [Fact]
    public void PostComposer_HasPostTypeSelect()
    {
        var cut = RenderComponent<PostComposer>(p => p
            .Add(x => x.NetworkName, "Test")
            .Add(x => x.NetworkId, "net1"));

        var select = cut.FindComponent<MudSelect<PostType>>();
        Assert.NotNull(select);
        Assert.Equal("Post Type", select.Instance.Label);
    }

    [Fact]
    public void LinkedInLinked_ShowsCrossPostToggle()
    {
        _linkedInService.Status = new LinkedInStatusResponse { IsLinked = true };

        var cut = RenderComponent<PostComposer>(p => p
            .Add(x => x.NetworkName, "Test")
            .Add(x => x.NetworkId, "net1"));

        Assert.Contains("Also share on LinkedIn", cut.Markup);
    }

    [Fact]
    public void LinkedInNotLinked_HidesCrossPostToggle()
    {
        _linkedInService.Status = new LinkedInStatusResponse { IsLinked = false };

        var cut = RenderComponent<PostComposer>(p => p
            .Add(x => x.NetworkName, "Test")
            .Add(x => x.NetworkId, "net1"));

        Assert.DoesNotContain("Also share on LinkedIn", cut.Markup);
    }
}
