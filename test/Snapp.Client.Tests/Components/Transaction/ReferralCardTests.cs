using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Transaction;
using Snapp.Client.Services;
using Snapp.Client.Tests.Mocks;
using Snapp.Shared.DTOs.Transaction;
using Snapp.Shared.Enums;
using Xunit;

namespace Snapp.Client.Tests.Components.Transaction;

public class ReferralCardTests : TestContext
{
    private readonly MockReferralService _referralService;

    public ReferralCardTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _referralService = new MockReferralService();
        Services.AddSingleton<IReferralService>(_referralService);
        Services.AddSingleton<ISnackbar>(Services.BuildServiceProvider().GetRequiredService<ISnackbar>());

        RenderComponent<MudPopoverProvider>();
    }

    private static ReferralResponse MakeReferral(ReferralStatus status = ReferralStatus.Created) => new()
    {
        ReferralId = "ref1",
        SenderUserId = "sender1",
        SenderDisplayName = "Alice",
        ReceiverUserId = "receiver1",
        ReceiverDisplayName = "Bob",
        NetworkId = "net1",
        Specialty = "Endodontics",
        Status = status,
        Notes = "Patient needs root canal.",
        CreatedAt = new DateTime(2026, 3, 15)
    };

    [Fact]
    public void ReferralCard_DisplaysSenderAndReceiver()
    {
        var referral = MakeReferral();
        var cut = RenderComponent<ReferralCard>(p => p
            .Add(x => x.Referral, referral)
            .Add(x => x.CurrentUserId, "sender1"));

        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
    }

    [Fact]
    public void ReferralCard_DisplaysSpecialty()
    {
        var referral = MakeReferral();
        var cut = RenderComponent<ReferralCard>(p => p
            .Add(x => x.Referral, referral)
            .Add(x => x.CurrentUserId, "sender1"));

        Assert.Contains("Endodontics", cut.Markup);
    }

    [Fact]
    public void ReferralCard_DisplaysStatusBadge()
    {
        var referral = MakeReferral(ReferralStatus.Accepted);
        var cut = RenderComponent<ReferralCard>(p => p
            .Add(x => x.Referral, referral)
            .Add(x => x.CurrentUserId, "sender1"));

        Assert.Contains("Accepted", cut.Markup);
    }

    [Fact]
    public void ReferralCard_ShowsAcceptButton_ForReceiver_WhenCreated()
    {
        var referral = MakeReferral(ReferralStatus.Created);
        var cut = RenderComponent<ReferralCard>(p => p
            .Add(x => x.Referral, referral)
            .Add(x => x.CurrentUserId, "receiver1"));

        Assert.Contains("Accept", cut.Markup);
    }

    [Fact]
    public void ReferralCard_HidesAcceptButton_ForSender()
    {
        var referral = MakeReferral(ReferralStatus.Created);
        var cut = RenderComponent<ReferralCard>(p => p
            .Add(x => x.Referral, referral)
            .Add(x => x.CurrentUserId, "sender1"));

        Assert.DoesNotContain("Accept", cut.Markup);
    }

    [Fact]
    public void ReferralCard_ShowsRecordOutcome_WhenAccepted()
    {
        var referral = MakeReferral(ReferralStatus.Accepted);
        var cut = RenderComponent<ReferralCard>(p => p
            .Add(x => x.Referral, referral)
            .Add(x => x.CurrentUserId, "sender1"));

        Assert.Contains("Record Outcome", cut.Markup);
    }

    [Fact]
    public void ReferralCard_HidesRecordOutcome_WhenCreated()
    {
        var referral = MakeReferral(ReferralStatus.Created);
        var cut = RenderComponent<ReferralCard>(p => p
            .Add(x => x.Referral, referral)
            .Add(x => x.CurrentUserId, "sender1"));

        Assert.DoesNotContain("Record Outcome", cut.Markup);
    }

    [Fact]
    public void ReferralCard_DisplaysDate()
    {
        var referral = MakeReferral();
        var cut = RenderComponent<ReferralCard>(p => p
            .Add(x => x.Referral, referral)
            .Add(x => x.CurrentUserId, "sender1"));

        Assert.Contains("Mar 15, 2026", cut.Markup);
    }
}
