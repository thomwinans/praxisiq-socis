using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.DealRoom;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Xunit;

namespace Snapp.Client.Tests.Components.DealRoom;

public class CreateDealRoomDialogTests : TestContext
{
    private readonly MockDealRoomService _dealRoomService;

    public CreateDealRoomDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _dealRoomService = new MockDealRoomService();
        Services.AddSingleton<IDealRoomService>(_dealRoomService);
    }

    [Fact]
    public async Task Dialog_RendersNameField()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogService.ShowAsync<CreateDealRoomDialog>("New Deal Room"));

        Assert.Contains("Deal Room Name", provider.Markup);
    }

    [Fact]
    public async Task Dialog_HasCreateButton()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogService.ShowAsync<CreateDealRoomDialog>("New Deal Room"));

        Assert.Contains("Create", provider.Markup);
    }

    [Fact]
    public async Task Dialog_HasCancelButton()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogService.ShowAsync<CreateDealRoomDialog>("New Deal Room"));

        Assert.Contains("Cancel", provider.Markup);
    }
}
