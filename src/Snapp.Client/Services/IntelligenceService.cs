using System.Net.Http.Json;
using Snapp.Shared.DTOs.Common;
using Snapp.Shared.DTOs.Intelligence;

namespace Snapp.Client.Services;

public class IntelligenceService : IIntelligenceService
{
    private readonly HttpClient _http;

    public IntelligenceService(HttpClient http)
    {
        _http = http;
    }

    public async Task<DashboardResponse?> GetDashboardAsync()
    {
        return await _http.GetFromJsonAsync<DashboardResponse>("intel/dashboard");
    }

    public async Task<ScoreResponse?> GetScoreAsync()
    {
        var response = await _http.GetAsync("intel/score");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ScoreResponse>();
    }

    public async Task<ContributionListResponse?> GetContributionsAsync()
    {
        return await _http.GetFromJsonAsync<ContributionListResponse>("intel/contributions");
    }

    public async Task<MessageResponse?> ContributeDataAsync(SubmitDataRequest request)
    {
        var response = await _http.PostAsJsonAsync("intel/contribute", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<MessageResponse>();
        return null;
    }

    public async Task<BenchmarkResponse?> GetBenchmarksAsync(string specialty, string geography, string sizeBand)
    {
        var url = $"intel/benchmarks?specialty={Uri.EscapeDataString(specialty)}&geo={Uri.EscapeDataString(geography)}&size={Uri.EscapeDataString(sizeBand)}";
        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<BenchmarkResponse>();
    }

    public async Task<ValuationResponse?> GetValuationAsync()
    {
        var response = await _http.GetAsync("intel/valuation");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ValuationResponse>();
    }

    public async Task<ValuationResponse?> ComputeScenarioAsync(Dictionary<string, string> overrides)
    {
        var response = await _http.PostAsJsonAsync("intel/valuation/scenario", new { Overrides = overrides });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ValuationResponse>();
    }

    public async Task<CareerStageResponse?> GetCareerStageAsync()
    {
        var response = await _http.GetAsync("intel/career-stage");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CareerStageResponse>();
    }

    public async Task<MarketProfileResponse?> GetMarketProfileAsync(string geoId)
    {
        var response = await _http.GetAsync($"intel/market/{Uri.EscapeDataString(geoId)}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MarketProfileResponse>();
    }

    public async Task<MarketCompareResponse?> CompareMarketsAsync(string[] geoIds)
    {
        var geos = string.Join(",", geoIds.Select(Uri.EscapeDataString));
        var response = await _http.GetAsync($"intel/market/compare?geos={geos}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MarketCompareResponse>();
    }

    public async Task<PendingQuestionsResponse?> GetPendingQuestionsAsync()
    {
        var response = await _http.GetAsync("intel/questions");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PendingQuestionsResponse>();
    }

    public async Task<AnswerQuestionResponse?> AnswerQuestionAsync(string questionId, string answer)
    {
        var response = await _http.PostAsJsonAsync($"intel/questions/{Uri.EscapeDataString(questionId)}/answer", new { Answer = answer });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AnswerQuestionResponse>();
    }

    public async Task<ProgressionResponse?> GetProgressionAsync()
    {
        var response = await _http.GetAsync("intel/questions/progression");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProgressionResponse>();
    }

    public async Task<VerticalConfigResponse?> GetVerticalConfigAsync()
    {
        // The vertical config is served from the intelligence service
        // For now, return a hardcoded dental config matching the server's dental-default.json
        // This will be replaced with an API call when the endpoint is available
        return await Task.FromResult(GetDentalConfig());
    }

    private static VerticalConfigResponse GetDentalConfig() => new()
    {
        Vertical = "dental",
        DisplayName = "Dental Practice",
        Dimensions = new()
        {
            new() { Name = "FinancialHealth", DisplayName = "Financial Health", Weight = 0.25m, Kpis = new()
            {
                new() { Name = "AnnualRevenue", DisplayName = "Annual Revenue", Unit = "USD", Category = "financial" },
                new() { Name = "OverheadRatio", DisplayName = "Overhead Ratio", Unit = "%", Category = "financial" },
                new() { Name = "CollectionsRate", DisplayName = "Collections Rate", Unit = "%", Category = "financial" },
                new() { Name = "ProfitMargin", DisplayName = "Profit Margin", Unit = "%", Category = "financial" },
                new() { Name = "RevenuePerProvider", DisplayName = "Revenue Per Provider", Unit = "USD", Category = "financial" },
            }},
            new() { Name = "OwnerRisk", DisplayName = "Owner / Key-Person Risk", Weight = 0.20m, Kpis = new()
            {
                new() { Name = "OwnerProductionPct", DisplayName = "Owner Production %", Unit = "%", Category = "owner_risk" },
                new() { Name = "ProviderCount", DisplayName = "Provider Count", Unit = "count", Category = "owner_risk" },
                new() { Name = "SuccessionPlanExists", DisplayName = "Succession Plan", Unit = "bool", Category = "owner_risk" },
                new() { Name = "KeyPersonDependency", DisplayName = "Key-Person Dependency Score", Unit = "score", Category = "owner_risk" },
            }},
            new() { Name = "Operations", DisplayName = "Operations", Weight = 0.20m, Kpis = new()
            {
                new() { Name = "ChairUtilization", DisplayName = "Chair Utilization", Unit = "%", Category = "operations" },
                new() { Name = "HygieneDaysPerWeek", DisplayName = "Hygiene Days / Week", Unit = "days", Category = "operations" },
                new() { Name = "NewPatientsPerMonth", DisplayName = "New Patients / Month", Unit = "count", Category = "operations" },
                new() { Name = "StaffToProviderRatio", DisplayName = "Staff to Provider Ratio", Unit = "ratio", Category = "operations" },
                new() { Name = "TreatmentAcceptanceRate", DisplayName = "Treatment Acceptance Rate", Unit = "%", Category = "operations" },
            }},
            new() { Name = "ClientBase", DisplayName = "Client Base", Weight = 0.15m, Kpis = new()
            {
                new() { Name = "ActivePatientCount", DisplayName = "Active Patients", Unit = "count", Category = "client_base" },
                new() { Name = "PatientRetentionRate", DisplayName = "Patient Retention Rate", Unit = "%", Category = "client_base" },
                new() { Name = "OnlineRating", DisplayName = "Online Rating", Unit = "stars", Category = "client_base" },
                new() { Name = "ReviewCount", DisplayName = "Review Count", Unit = "count", Category = "client_base" },
            }},
            new() { Name = "RevenueDiversification", DisplayName = "Revenue Diversification", Weight = 0.10m, Kpis = new()
            {
                new() { Name = "InsuranceMixPct", DisplayName = "Insurance Mix %", Unit = "%", Category = "revenue_mix" },
                new() { Name = "FeeForServicePct", DisplayName = "Fee-for-Service %", Unit = "%", Category = "revenue_mix" },
                new() { Name = "SpecialtyServicePct", DisplayName = "Specialty Service Revenue %", Unit = "%", Category = "revenue_mix" },
                new() { Name = "TopPayerConcentration", DisplayName = "Top Payer Concentration", Unit = "%", Category = "revenue_mix" },
            }},
            new() { Name = "MarketPosition", DisplayName = "Market Position", Weight = 0.10m, Kpis = new()
            {
                new() { Name = "PractitionerDensity", DisplayName = "Dentists per 100K Pop", Unit = "ratio", Category = "market" },
                new() { Name = "CompetitorCount", DisplayName = "Competitors in Area", Unit = "count", Category = "market" },
                new() { Name = "DsoPresence", DisplayName = "DSO Presence", Unit = "bool", Category = "market" },
                new() { Name = "PopulationGrowthRate", DisplayName = "Population Growth Rate", Unit = "%", Category = "market" },
            }},
        },
        ContributionCategories = new()
        {
            new() { Category = "financial", Dimension = "FinancialHealth", ConfidenceWeight = 0.15m, DisplayName = "Financial Data" },
            new() { Category = "owner_risk", Dimension = "OwnerRisk", ConfidenceWeight = 0.12m, DisplayName = "Owner & Key-Person Data" },
            new() { Category = "operations", Dimension = "Operations", ConfidenceWeight = 0.12m, DisplayName = "Operational Data" },
            new() { Category = "client_base", Dimension = "ClientBase", ConfidenceWeight = 0.10m, DisplayName = "Client Base Data" },
            new() { Category = "revenue_mix", Dimension = "RevenueDiversification", ConfidenceWeight = 0.08m, DisplayName = "Revenue Mix Data" },
            new() { Category = "market", Dimension = "MarketPosition", ConfidenceWeight = 0.08m, DisplayName = "Market Position Data" },
        },
    };
}
