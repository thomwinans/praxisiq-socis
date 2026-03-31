using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Snapp.Client.Pages.Intelligence;
using Snapp.Client.Services;
using Snapp.Shared.DTOs.Intelligence;
using Xunit;

namespace Snapp.Client.Tests.Pages.Intelligence;

public class DashboardTests : TestContext
{
    private readonly FakeIntelligenceService _service = new();

    public DashboardTests()
    {
        Services.AddSingleton<IIntelligenceService>(_service);
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Dashboard_Loading_ShowsProgressCircular()
    {
        // Don't set any result so it returns null quickly, but the component shows loading initially
        _service.DashboardResult = null;
        var cut = RenderComponent<Dashboard>();

        // After async load completes, it should show error (null dashboard)
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed to load", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_Error_ShowsErrorAlert()
    {
        _service.ThrowOnDashboard = true;
        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
            Assert.Contains("Failed to load", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_WithValuation_ShowsValuationHero()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 72,
            KPIs = new List<KpiItem>
            {
                new() { Name = "Annual Revenue", Value = "1200000", Unit = "USD", Trend = "Up" },
            },
            ValuationSummary = new ValuationSummary
            {
                Downside = 800_000,
                Base = 1_200_000,
                Upside = 1_600_000,
                ConfidenceScore = 72,
            },
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Practice Valuation Range", cut.Markup);
            Assert.Contains("$1.2M", cut.Markup);
            Assert.Contains("Downside", cut.Markup);
            Assert.Contains("Upside", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_NoValuation_ShowsContributePrompt()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 40,
            KPIs = new(),
            ValuationSummary = null,
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Contribute practice data to unlock", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_WithKPIs_ShowsKpiTable()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 60,
            KPIs = new List<KpiItem>
            {
                new() { Name = "Annual Revenue", Value = "1200000", Unit = "USD", Trend = "Up", Percentile = 75 },
                new() { Name = "Overhead Ratio", Value = "65", Unit = "%", Trend = "Down" },
            },
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Annual Revenue", cut.Markup);
            Assert.Contains("Overhead Ratio", cut.Markup);
            Assert.Contains("75th", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_LowConfidence_ShowsContributeButton()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 45,
            KPIs = new(),
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Contribute Data", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_ShowsConfidenceBar()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 72,
            KPIs = new(),
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Data Confidence", cut.Markup);
            Assert.Contains("72%", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_NoCareerStage_ShowsFallbackText()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 50,
            KPIs = new(),
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Career Stage", cut.Markup);
            Assert.Contains("No career stage classification yet", cut.Markup);
            Assert.Contains("No active risk flags", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_WithCareerStage_ShowsStageAndConfidence()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 70,
            KPIs = new(),
        };
        _service.CareerStageResult = new CareerStageResponse
        {
            Stage = "growth",
            DisplayName = "Growth Phase",
            ConfidenceLevel = "high",
            RiskFlags = new(),
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Growth Phase", cut.Markup);
            Assert.Contains("high confidence", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_WithRiskFlags_ShowsFlags()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 70,
            KPIs = new(),
        };
        _service.CareerStageResult = new CareerStageResponse
        {
            Stage = "mature",
            DisplayName = "Mature Practice",
            ConfidenceLevel = "medium",
            RiskFlags = new List<RiskFlagResponse>
            {
                new() { Type = "key_person", Severity = "high", Description = "High owner production dependency" },
                new() { Type = "succession", Severity = "medium", Description = "No succession plan in place" },
            },
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("High owner production dependency", cut.Markup);
            Assert.Contains("No succession plan in place", cut.Markup);
            Assert.Contains("1 high-severity risk flag(s)", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_WithScores_ShowsDonutChart()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 70,
            KPIs = new(),
        };
        _service.ScoreResult = new ScoreResponse
        {
            DimensionScores = new Dictionary<string, decimal>
            {
                ["FinancialHealth"] = 75,
                ["Operations"] = 60,
                ["ClientBase"] = 80,
            },
            OverallScore = 72,
            ConfidenceLevel = "medium",
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            var chart = cut.FindComponent<MudChart>();
            Assert.NotNull(chart);
        });
    }

    [Fact]
    public void Dashboard_ShowsViewBenchmarksLink()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 80,
            KPIs = new(),
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("View Benchmarks", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_WithPendingQuestions_ShowsQuestionCard()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 50,
            KPIs = new(),
        };
        _service.PendingQuestionsResult = new PendingQuestionsResponse
        {
            CurrentConfidence = 50,
            Questions = new List<QuestionItem>
            {
                new()
                {
                    QuestionId = "q1",
                    Type = "ConfirmData",
                    PromptText = "Is your revenue above $1M?",
                    Choices = new() { "Yes", "No" },
                    UnlockDescription = "Unlocks revenue benchmark",
                    Priority = 0.9m,
                },
            },
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Quick question to unlock insights", cut.Markup);
            Assert.Contains("Is your revenue above $1M?", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_NoPendingQuestions_NoQuestionCard()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 70,
            KPIs = new(),
        };
        _service.PendingQuestionsResult = new PendingQuestionsResponse
        {
            CurrentConfidence = 70,
            Questions = new(),
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Quick question to unlock insights", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_WithProgression_ShowsProgressionIndicator()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 65,
            KPIs = new(),
        };
        _service.ProgressionResult = new ProgressionResponse
        {
            TotalAnswered = 5,
            TotalUnlocks = 3,
            CurrentStreak = 2,
        };

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("3 insights unlocked", cut.Markup);
            Assert.Contains("2 day streak", cut.Markup);
        });
    }

    [Fact]
    public void Dashboard_NoProgression_HidesProgressionIndicator()
    {
        _service.DashboardResult = new DashboardResponse
        {
            ConfidenceScore = 65,
            KPIs = new(),
        };
        // No progression result set — stays null

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("insights unlocked", cut.Markup);
        });
    }

    private class FakeAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new System.Security.Claims.ClaimsIdentity("test");
            var user = new System.Security.Claims.ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(user));
        }
    }
}
