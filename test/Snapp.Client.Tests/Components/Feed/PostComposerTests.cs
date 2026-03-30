using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Feed;
using Snapp.Shared.Enums;
using Xunit;

namespace Snapp.Client.Tests.Components.Feed;

public class PostComposerTests : TestContext
{
    public PostComposerTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void PostComposer_RendersTextFieldAndButton()
    {
        var cut = RenderComponent<PostComposer>(p =>
            p.Add(x => x.NetworkName, "Test Network"));

        var textField = cut.FindComponent<MudTextField<string>>();
        Assert.NotNull(textField);
        Assert.Contains("Post", cut.Markup);
    }

    [Fact]
    public void PostComposer_ShowsNetworkName()
    {
        var cut = RenderComponent<PostComposer>(p =>
            p.Add(x => x.NetworkName, "My Guild"));

        Assert.Contains("Posting to:", cut.Markup);
        Assert.Contains("My Guild", cut.Markup);
    }

    [Fact]
    public void PostComposer_SubmitButton_DisabledWhenEmpty()
    {
        var cut = RenderComponent<PostComposer>(p =>
            p.Add(x => x.NetworkName, "Test"));

        var buttons = cut.FindComponents<MudButton>();
        var submitButton = buttons.First(b => b.Markup.Contains("Post"));
        Assert.True(submitButton.Instance.Disabled);
    }

    [Fact]
    public void PostComposer_HasPostTypeSelect()
    {
        var cut = RenderComponent<PostComposer>(p =>
            p.Add(x => x.NetworkName, "Test"));

        var select = cut.FindComponent<MudSelect<PostType>>();
        Assert.NotNull(select);
        Assert.Equal("Post Type", select.Instance.Label);
    }
}
