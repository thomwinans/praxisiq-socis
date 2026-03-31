using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.DealRoom;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Xunit;

namespace Snapp.Client.Tests.Components.DealRoom;

public class AddParticipantDialogTests : TestContext
{
    private readonly MockDealRoomService _dealRoomService;

    public AddParticipantDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _dealRoomService = new MockDealRoomService();
        Services.AddSingleton<IDealRoomService>(_dealRoomService);
    }

    [Fact]
    public async Task Dialog_RendersUserIdField()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<AddParticipantDialog>
        {
            { x => x.DealId, "deal1" }
        };
        await provider.InvokeAsync(() => dialogService.ShowAsync<AddParticipantDialog>("Add Participant", parameters));

        Assert.Contains("User ID", provider.Markup);
    }

    [Fact]
    public async Task Dialog_RendersRoleSelector()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<AddParticipantDialog>
        {
            { x => x.DealId, "deal1" }
        };
        await provider.InvokeAsync(() => dialogService.ShowAsync<AddParticipantDialog>("Add Participant", parameters));

        Assert.Contains("Role", provider.Markup);
    }

    [Fact]
    public async Task Dialog_HasAddParticipantButton()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<AddParticipantDialog>
        {
            { x => x.DealId, "deal1" }
        };
        await provider.InvokeAsync(() => dialogService.ShowAsync<AddParticipantDialog>("Add Participant", parameters));

        Assert.Contains("Add Participant", provider.Markup);
    }

    [Fact]
    public async Task Dialog_HasCancelButton()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<AddParticipantDialog>
        {
            { x => x.DealId, "deal1" }
        };
        await provider.InvokeAsync(() => dialogService.ShowAsync<AddParticipantDialog>("Add Participant", parameters));

        Assert.Contains("Cancel", provider.Markup);
    }
}
