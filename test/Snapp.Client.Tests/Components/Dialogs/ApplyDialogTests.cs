using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Dialogs;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Xunit;

namespace Snapp.Client.Tests.Components.Dialogs;

public class ApplyDialogTests : TestContext
{
    public ApplyDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<INetworkService>(new MockNetworkService());

        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
    }

    [Fact]
    public void ApplyDialog_CanBeOpened()
    {
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ApplyDialog>
        {
            { x => x.NetworkId, "net1" },
            { x => x.NetworkName, "Test Network" },
        };

        var comp = RenderComponent<MudDialogProvider>();
        comp.InvokeAsync(async () =>
        {
            await dialogService.ShowAsync<ApplyDialog>("Apply", parameters);
        });

        Assert.Contains("Submit Application", comp.Markup);
        Assert.Contains("Cancel", comp.Markup);
    }
}
