using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Discussion;
using Xunit;

namespace Snapp.Client.Tests.Components.Discussion;

public class ReplyComposerTests : TestContext
{
    public ReplyComposerTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ReplyComposer_RendersTextFieldAndButton()
    {
        var cut = RenderComponent<ReplyComposer>();

        var textField = cut.FindComponent<MudTextField<string>>();
        Assert.NotNull(textField);
        Assert.Equal("Write a reply...", textField.Instance.Label);

        Assert.Contains("Reply", cut.Markup);
    }

    [Fact]
    public void ReplyComposer_SubmitButton_DisabledWhenEmpty()
    {
        var cut = RenderComponent<ReplyComposer>();

        var buttons = cut.FindComponents<MudButton>();
        var submitButton = buttons.First(b => b.Markup.Contains("Reply"));
        Assert.True(submitButton.Instance.Disabled);
    }

    [Fact]
    public void ReplyComposer_ShowsHelperText()
    {
        var cut = RenderComponent<ReplyComposer>();
        Assert.Contains("Shift+Enter for newline", cut.Markup);
    }
}
