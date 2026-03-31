using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Transaction;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Xunit;

namespace Snapp.Client.Tests.Components.Transaction;

public class RecordOutcomeDialogTests : TestContext
{
    public RecordOutcomeDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IReferralService>(new MockReferralService());

        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
    }

    [Fact]
    public void RecordOutcomeDialog_CanBeOpened()
    {
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<RecordOutcomeDialog>
        {
            { x => x.ReferralId, "ref1" },
            { x => x.ReferralSender, "Alice" },
            { x => x.ReferralReceiver, "Bob" },
        };

        var comp = RenderComponent<MudDialogProvider>();
        comp.InvokeAsync(async () =>
        {
            await dialogService.ShowAsync<RecordOutcomeDialog>("Record Outcome", parameters);
        });

        Assert.Contains("Record Outcome", comp.Markup);
        Assert.Contains("Alice", comp.Markup);
        Assert.Contains("Bob", comp.Markup);
    }

    [Fact]
    public void RecordOutcomeDialog_HasOutcomeFieldAndSuccessCheckbox()
    {
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<RecordOutcomeDialog>
        {
            { x => x.ReferralId, "ref1" },
            { x => x.ReferralSender, "Alice" },
            { x => x.ReferralReceiver, "Bob" },
        };

        var comp = RenderComponent<MudDialogProvider>();
        comp.InvokeAsync(async () =>
        {
            await dialogService.ShowAsync<RecordOutcomeDialog>("Record Outcome", parameters);
        });

        Assert.Contains("Outcome", comp.Markup);
        Assert.Contains("Successful outcome", comp.Markup);
    }
}
