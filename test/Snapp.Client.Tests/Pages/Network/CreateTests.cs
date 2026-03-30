using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Network;
using Snapp.Client.Services;
using Snapp.Client.State;
using Snapp.Client.Tests.Mocks;
using Xunit;

namespace Snapp.Client.Tests.Pages.Network;

public class CreateTests : TestContext
{
    public CreateTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<INetworkService>(new MockNetworkService());
        Services.AddScoped(_ => new NetworkState(JSInterop.JSRuntime));

        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Create_RendersTitle()
    {
        var cut = RenderComponent<Create>();
        Assert.Contains("Create Network", cut.Markup);
    }

    [Fact]
    public void Create_HasStepperWithThreeSteps()
    {
        var cut = RenderComponent<Create>();

        Assert.Contains("Basics", cut.Markup);
        Assert.Contains("Charter", cut.Markup);
        Assert.Contains("Template", cut.Markup);
    }

    [Fact]
    public void Create_HasNameField()
    {
        var cut = RenderComponent<Create>();
        Assert.Contains("Network Name", cut.Markup);
    }

    [Fact]
    public void Create_HasNavigationButtons()
    {
        var cut = RenderComponent<Create>();

        Assert.Contains("Back", cut.Markup);
        Assert.Contains("Next", cut.Markup);
    }
}
