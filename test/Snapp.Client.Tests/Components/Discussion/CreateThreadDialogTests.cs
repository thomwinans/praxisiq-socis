using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Discussion;
using Snapp.Client.Services;
using Snapp.Shared.DTOs.Content;
using Xunit;

namespace Snapp.Client.Tests.Components.Discussion;

public class CreateThreadDialogTests : TestContext
{
    private readonly FakeDialogDiscussionService _discussionService = new();

    public CreateThreadDialogTests()
    {
        Services.AddSingleton<IDiscussionService>(_discussionService);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void CreateThreadDialog_RendersFormFields()
    {
        var cut = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<CreateThreadDialog>
        {
            { x => x.NetworkId, "net1" }
        };

        cut.InvokeAsync(() => dialogService.ShowAsync<CreateThreadDialog>("New Thread", parameters));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Title", cut.Markup);
            Assert.Contains("Content", cut.Markup);
            Assert.Contains("Create Thread", cut.Markup);
            Assert.Contains("Cancel", cut.Markup);
        });
    }

    private class FakeDialogDiscussionService : IDiscussionService
    {
        public Task<ThreadListResponse> GetThreadsAsync(string networkId, string? nextToken = null, int limit = 25)
            => Task.FromResult(new ThreadListResponse());
        public Task<ThreadResponse?> GetThreadAsync(string threadId)
            => Task.FromResult<ThreadResponse?>(null);
        public Task<ThreadResponse?> CreateThreadAsync(string networkId, CreateThreadRequest request)
            => Task.FromResult<ThreadResponse?>(new ThreadResponse
            {
                ThreadId = "new-thread",
                NetworkId = networkId,
                Title = request.Title,
                CreatedAt = DateTime.UtcNow,
            });
        public Task<ReplyListResponse> GetRepliesAsync(string threadId, string? nextToken = null, int limit = 50)
            => Task.FromResult(new ReplyListResponse());
        public Task<ReplyResponse?> CreateReplyAsync(string threadId, CreateReplyRequest request)
            => Task.FromResult<ReplyResponse?>(null);
        public Task<bool> DeleteReplyAsync(string threadId, string replyId)
            => Task.FromResult(true);
    }
}
