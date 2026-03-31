using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Components.Intelligence;
using Snapp.Client.Services;
using Snapp.Client.Tests.Pages.Intelligence;
using Xunit;

namespace Snapp.Client.Tests.Components.Intelligence;

public class QuestionCardTests : TestContext
{
    private readonly FakeIntelligenceService _service = new();

    public QuestionCardTests()
    {
        Services.AddSingleton<IIntelligenceService>(_service);
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    private static QuestionItem MakeBooleanQuestion() => new()
    {
        QuestionId = "q1",
        Type = "ConfirmData",
        Category = "financial",
        PromptText = "Is your annual revenue above $1M?",
        Choices = new List<string> { "Yes", "No" },
        UnlockDescription = "Unlocks revenue benchmark comparison",
        Priority = 0.9m,
    };

    private static QuestionItem MakeChoiceQuestion() => new()
    {
        QuestionId = "q2",
        Type = "EstimateValue",
        Category = "operations",
        PromptText = "How many operatories does your practice have?",
        Choices = new List<string> { "1-3", "4-6", "7-10", "10+" },
        UnlockDescription = "Unlocks chair utilization insights",
        Priority = 0.8m,
    };

    [Fact]
    public void QuestionCard_RendersPromptText()
    {
        var question = MakeBooleanQuestion();
        var cut = RenderComponent<QuestionCard>(p => p.Add(c => c.Question, question));

        Assert.Contains("Is your annual revenue above $1M?", cut.Markup);
        Assert.Contains("Quick question to unlock insights", cut.Markup);
    }

    [Fact]
    public void QuestionCard_Boolean_ShowsYesNoButtons()
    {
        var question = MakeBooleanQuestion();
        var cut = RenderComponent<QuestionCard>(p => p.Add(c => c.Question, question));

        Assert.Contains("Yes", cut.Markup);
        Assert.Contains("No", cut.Markup);
    }

    [Fact]
    public void QuestionCard_Choices_ShowsRadioGroup()
    {
        var question = MakeChoiceQuestion();
        var cut = RenderComponent<QuestionCard>(p => p.Add(c => c.Question, question));

        Assert.Contains("1-3", cut.Markup);
        Assert.Contains("4-6", cut.Markup);
        Assert.Contains("7-10", cut.Markup);
        Assert.Contains("10+", cut.Markup);
    }

    [Fact]
    public void QuestionCard_AnswerButton_DisabledWithoutSelection()
    {
        var question = MakeBooleanQuestion();
        var cut = RenderComponent<QuestionCard>(p => p.Add(c => c.Question, question));

        var buttons = cut.FindComponents<MudButton>();
        var answerButton = buttons.First(b => b.Markup.Contains("Answer"));
        Assert.True(answerButton.Instance.Disabled);
    }

    [Fact]
    public void QuestionCard_ShowsSkipButton()
    {
        var question = MakeBooleanQuestion();
        var cut = RenderComponent<QuestionCard>(p => p.Add(c => c.Question, question));

        Assert.Contains("Skip", cut.Markup);
    }

    [Fact]
    public async Task QuestionCard_OnAnswer_ShowsSuccessAlert()
    {
        _service.AnswerQuestionResult = new AnswerQuestionResponse
        {
            QuestionId = "q1",
            Accepted = true,
            UnlockDescription = "Revenue benchmark unlocked!",
            UnlockType = "data_confirmed",
            ConfidenceAfter = 55,
            Progression = new ProgressionSummary { TotalAnswered = 1, TotalUnlocks = 1, CurrentStreak = 1 },
        };

        var answeredCalled = false;
        var question = MakeBooleanQuestion();
        var cut = RenderComponent<QuestionCard>(p =>
        {
            p.Add(c => c.Question, question);
            p.Add(c => c.OnAnswered, (AnswerQuestionResponse? _) => { answeredCalled = true; });
        });

        // Click "Yes" button to select answer
        var yesButton = cut.FindAll("button").First(b => b.TextContent.Contains("Yes"));
        await cut.InvokeAsync(() => yesButton.Click());

        // Click "Answer" button
        var answerButton = cut.FindAll("button").First(b => b.TextContent.Contains("Answer"));
        await cut.InvokeAsync(() => answerButton.Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Revenue benchmark unlocked!", cut.Markup);
            Assert.True(answeredCalled);
        });
    }

    [Fact]
    public async Task QuestionCard_OnSkip_InvokesCallback()
    {
        var skippedCalled = false;
        var question = MakeBooleanQuestion();
        var cut = RenderComponent<QuestionCard>(p =>
        {
            p.Add(c => c.Question, question);
            p.Add(c => c.OnSkipped, () => { skippedCalled = true; });
        });

        var skipButton = cut.FindAll("button").First(b => b.TextContent.Contains("Skip"));
        await cut.InvokeAsync(() => skipButton.Click());

        Assert.True(skippedCalled);
    }

    [Fact]
    public async Task QuestionCard_OnAnswer_SendsCorrectData()
    {
        _service.AnswerQuestionResult = new AnswerQuestionResponse
        {
            QuestionId = "q1",
            Accepted = true,
        };

        var question = MakeBooleanQuestion();
        var cut = RenderComponent<QuestionCard>(p => p.Add(c => c.Question, question));

        // Select "No"
        var noButton = cut.FindAll("button").First(b => b.TextContent.Contains("No"));
        await cut.InvokeAsync(() => noButton.Click());

        // Submit
        var answerButton = cut.FindAll("button").First(b => b.TextContent.Contains("Answer"));
        await cut.InvokeAsync(() => answerButton.Click());

        Assert.Equal("q1", _service.LastAnsweredQuestionId);
        Assert.Equal("No", _service.LastAnswer);
    }
}
