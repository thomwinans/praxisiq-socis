using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Discussion;
using Snapp.Client.Services;
using Snapp.Shared.DTOs.Content;
using Xunit;

namespace Snapp.Client.Tests.Pages.Discussion;

public class ListTests : TestContext
{
    private readonly FakeDiscussionService _discussionService = new();

    public ListTests()
    {
        Services.AddSingleton<IDiscussionService>(_discussionService);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void List_Loading_ShowsSpinner()
    {
        _discussionService.DelayMs = 5000;
        var cut = RenderComponent<List>(p => p.Add(x => x.NetId, "net1"));
        Assert.NotNull(cut.FindComponent<MudProgressCircular>());
    }

    [Fact]
    public void List_Empty_ShowsEmptyState()
    {
        _discussionService.ThreadListResult = new ThreadListResponse { Threads = new() };
        var cut = RenderComponent<List>(p => p.Add(x => x.NetId, "net1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No discussions yet", cut.Markup);
        });
    }

    [Fact]
    public void List_WithThreads_RendersThreadItems()
    {
        _discussionService.ThreadListResult = new ThreadListResponse
        {
            Threads = new List<ThreadResponse>
            {
                new()
                {
                    ThreadId = "t1",
                    NetworkId = "net1",
                    Title = "First Thread",
                    AuthorDisplayName = "Alice",
                    ReplyCount = 3,
                    LastReplyAt = DateTime.UtcNow.AddMinutes(-5),
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                },
                new()
                {
                    ThreadId = "t2",
                    NetworkId = "net1",
                    Title = "Second Thread",
                    AuthorDisplayName = "Bob",
                    ReplyCount = 0,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                },
            },
        };

        var cut = RenderComponent<List>(p => p.Add(x => x.NetId, "net1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("First Thread", cut.Markup);
            Assert.Contains("Second Thread", cut.Markup);
            Assert.Contains("Alice", cut.Markup);
            Assert.Contains("Bob", cut.Markup);
        });
    }

    [Fact]
    public void List_WithNextToken_ShowsLoadMoreButton()
    {
        _discussionService.ThreadListResult = new ThreadListResponse
        {
            Threads = new List<ThreadResponse>
            {
                new() { ThreadId = "t1", Title = "Thread 1", AuthorDisplayName = "A", CreatedAt = DateTime.UtcNow },
            },
            NextToken = "token123",
        };

        var cut = RenderComponent<List>(p => p.Add(x => x.NetId, "net1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Load More", cut.Markup);
        });
    }

    [Fact]
    public void List_Error_ShowsRetryButton()
    {
        _discussionService.ThrowOnGetThreads = true;
        var cut = RenderComponent<List>(p => p.Add(x => x.NetId, "net1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed to load discussions", cut.Markup);
            Assert.Contains("Retry", cut.Markup);
        });
    }

    [Fact]
    public void List_NewThreadButton_Rendered()
    {
        _discussionService.ThreadListResult = new ThreadListResponse { Threads = new() };
        var cut = RenderComponent<List>(p => p.Add(x => x.NetId, "net1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("New Thread", cut.Markup);
        });
    }
}

internal class FakeDiscussionService : IDiscussionService
{
    public ThreadListResponse ThreadListResult { get; set; } = new() { Threads = new() };
    public ThreadResponse? ThreadResult { get; set; }
    public ThreadResponse? CreateThreadResult { get; set; }
    public ReplyListResponse ReplyListResult { get; set; } = new() { Replies = new() };
    public ReplyResponse? CreateReplyResult { get; set; }
    public bool DeleteReplyResult { get; set; } = true;
    public bool ThrowOnGetThreads { get; set; }
    public bool ThrowOnGetThread { get; set; }
    public bool ThrowOnCreateReply { get; set; }
    public int DelayMs { get; set; }

    public async Task<ThreadListResponse> GetThreadsAsync(string networkId, string? nextToken = null, int limit = 25)
    {
        if (DelayMs > 0) await Task.Delay(DelayMs);
        if (ThrowOnGetThreads) throw new HttpRequestException("Failed");
        return ThreadListResult;
    }

    public async Task<ThreadResponse?> GetThreadAsync(string threadId)
    {
        if (DelayMs > 0) await Task.Delay(DelayMs);
        if (ThrowOnGetThread) throw new HttpRequestException("Failed");
        return ThreadResult;
    }

    public Task<ThreadResponse?> CreateThreadAsync(string networkId, CreateThreadRequest request)
        => Task.FromResult(CreateThreadResult);

    public async Task<ReplyListResponse> GetRepliesAsync(string threadId, string? nextToken = null, int limit = 50)
    {
        if (DelayMs > 0) await Task.Delay(DelayMs);
        return ReplyListResult;
    }

    public Task<ReplyResponse?> CreateReplyAsync(string threadId, CreateReplyRequest request)
    {
        if (ThrowOnCreateReply) throw new HttpRequestException("Failed");
        return Task.FromResult(CreateReplyResult);
    }

    public Task<bool> DeleteReplyAsync(string threadId, string replyId)
        => Task.FromResult(DeleteReplyResult);
}
