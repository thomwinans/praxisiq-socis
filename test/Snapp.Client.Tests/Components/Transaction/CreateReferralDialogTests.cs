using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Transaction;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Snapp.Shared.DTOs.Network;
using Xunit;

namespace Snapp.Client.Tests.Components.Transaction;

public class CreateReferralDialogTests : TestContext
{
    public CreateReferralDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var networkService = new MockNetworkService
        {
            MyNetworks = new NetworkListResponse
            {
                Networks = new List<NetworkResponse>
                {
                    new() { NetworkId = "net1", Name = "Test Network" }
                }
            }
        };
        Services.AddSingleton<INetworkService>(networkService);
        Services.AddSingleton<IReferralService>(new MockReferralService());

        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
    }

    [Fact]
    public void CreateReferralDialog_CanBeOpened()
    {
        var dialogService = Services.GetRequiredService<IDialogService>();

        var comp = RenderComponent<MudDialogProvider>();
        comp.InvokeAsync(async () =>
        {
            await dialogService.ShowAsync<CreateReferralDialog>("New Referral");
        });

        Assert.Contains("Send Referral", comp.Markup);
        Assert.Contains("Cancel", comp.Markup);
    }

    [Fact]
    public void CreateReferralDialog_HasRequiredFields()
    {
        var dialogService = Services.GetRequiredService<IDialogService>();

        var comp = RenderComponent<MudDialogProvider>();
        comp.InvokeAsync(async () =>
        {
            await dialogService.ShowAsync<CreateReferralDialog>("New Referral");
        });

        Assert.Contains("Network", comp.Markup);
        Assert.Contains("Receiver", comp.Markup);
        Assert.Contains("Specialty", comp.Markup);
        Assert.Contains("Notes", comp.Markup);
    }
}
