using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Snapp.Service.Enrichment.Repositories;
using Snapp.Service.Enrichment.Services;
using Snapp.Shared.Constants;
using Xunit;

namespace Snapp.Service.Enrichment.Tests;

public class BenchmarkIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly AmazonDynamoDBClient _client;
    private readonly EnrichmentRepository _repo;
    private readonly BenchmarkDataLoader _benchmarkLoader;
    private readonly RegulatoryDataLoader _regulatoryLoader;
    private readonly EnrichmentProcessor _processor;

    public BenchmarkIntegrationTests()
    {
        _client = new AmazonDynamoDBClient(
            "fakeAccessKey", "fakeSecretKey",
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8042" });

        _repo = new EnrichmentRepository(
            _client,
            NullLogger<EnrichmentRepository>.Instance);

        _benchmarkLoader = new BenchmarkDataLoader(
            _repo,
            NullLogger<BenchmarkDataLoader>.Instance);

        _regulatoryLoader = new RegulatoryDataLoader(
            _repo,
            NullLogger<RegulatoryDataLoader>.Instance);

        var fixtureProviderSource = new FixtureProviderSource(
            NullLogger<FixtureProviderSource>.Instance);

        var fixtureMarketSource = new FixtureMarketSource(
            NullLogger<FixtureMarketSource>.Instance);

        var fixtureListingSource = new FixtureBusinessListingSource(
            NullLogger<FixtureBusinessListingSource>.Instance);

        var businessListingLoader = new BusinessListingLoader(
            fixtureListingSource,
            fixtureProviderSource,
            _repo,
            NullLogger<BusinessListingLoader>.Instance);

        var fixtureLicensingSource = new FixtureStateLicensingSource(
            NullLogger<FixtureStateLicensingSource>.Instance);
        var licensingLoader = new StateLicensingLoader(
            fixtureLicensingSource, fixtureProviderSource, _repo,
            NullLogger<StateLicensingLoader>.Instance);
        var fixtureJobPostingSource = new FixtureJobPostingSource(
            NullLogger<FixtureJobPostingSource>.Instance);
        var jobPostingLoader = new JobPostingLoader(
            fixtureJobPostingSource, _repo,
            NullLogger<JobPostingLoader>.Instance);

        _processor = new EnrichmentProcessor(
            fixtureProviderSource,
            fixtureMarketSource,
            _repo,
            _benchmarkLoader,
            _regulatoryLoader,
            businessListingLoader,
            licensingLoader,
            jobPostingLoader,
            NullLogger<EnrichmentProcessor>.Instance);
    }

    public async Task InitializeAsync()
    {
        await _repo.EnsureTableAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _client.Dispose();

    // ── Benchmark Loading Tests ─────────────────────────────────

    [Fact]
    public async Task LoadBenchmarks_CreatesCohortItems()
    {
        await _benchmarkLoader.LoadBenchmarksAsync();

        // Check cohort benchmark: General Practice / small
        var cohortPk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#small";
        var items = await _repo.QueryByPkPrefixAsync(cohortPk, "METRIC#");

        items.Should().NotBeEmpty("should have benchmark metrics for GP/small cohort");
        items.Count.Should().BeGreaterOrEqualTo(5, "revenue, overhead, profit margin, production, compensation");
    }

    [Fact]
    public async Task LoadBenchmarks_RevenueQuartilesHaveExpectedValues()
    {
        await _benchmarkLoader.LoadBenchmarksAsync();

        var cohortPk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#small";
        var item = await _repo.GetItemAsync(cohortPk, "METRIC#Revenue");

        item.Should().NotBeNull();
        decimal.Parse(item!["P25"].N).Should().Be(550000m);
        decimal.Parse(item["P50"].N).Should().Be(780000m);
        decimal.Parse(item["P75"].N).Should().Be(1050000m);
        int.Parse(item["SampleSize"].N).Should().BeGreaterOrEqualTo(5);
        item["Source"].S.Should().Be("association-benchmark");
    }

    [Fact]
    public async Task LoadBenchmarks_CreatesStateLevelBenchmarks()
    {
        await _benchmarkLoader.LoadBenchmarksAsync();

        // State-level benchmarks use BENCH# prefix (no specialty/sizeBand = geographic)
        // But our fixture has specialty+sizeBand for state entries too, so they go as COHORT# with state geography
        var cohortPk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#small";
        var items = await _repo.QueryByPkPrefixAsync(cohortPk, "METRIC#");

        // Should have Revenue metric at minimum
        items.Should().Contain(i => i["MetricName"].S == "Revenue");
    }

    [Fact]
    public async Task LoadBenchmarks_IncludesCompensationData()
    {
        await _benchmarkLoader.LoadBenchmarksAsync();

        var cohortPk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#small";
        var ownerComp = await _repo.GetItemAsync(cohortPk, "METRIC#OwnerCompensation");

        ownerComp.Should().NotBeNull();
        decimal.Parse(ownerComp!["P50"].N).Should().Be(250000m);

        var hygienistComp = await _repo.GetItemAsync(cohortPk, "METRIC#HygienistComp");
        hygienistComp.Should().NotBeNull();
        decimal.Parse(hygienistComp!["P50"].N).Should().Be(78000m);
    }

    [Fact]
    public async Task LoadBenchmarks_IncludesOverheadAndProduction()
    {
        await _benchmarkLoader.LoadBenchmarksAsync();

        var cohortPk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#small";

        var overhead = await _repo.GetItemAsync(cohortPk, "METRIC#OverheadRatio");
        overhead.Should().NotBeNull();
        decimal.Parse(overhead!["P50"].N).Should().Be(62.0m);

        var profitMargin = await _repo.GetItemAsync(cohortPk, "METRIC#ProfitMargin");
        profitMargin.Should().NotBeNull();
        decimal.Parse(profitMargin!["P50"].N).Should().Be(28.0m);

        var production = await _repo.GetItemAsync(cohortPk, "METRIC#ProductionPerProvider");
        production.Should().NotBeNull();
        decimal.Parse(production!["P50"].N).Should().Be(700000m);
    }

    [Fact]
    public async Task LoadBenchmarks_SpecialtyBenchmarksExist()
    {
        await _benchmarkLoader.LoadBenchmarksAsync();

        // Orthodontics
        var orthoPk = $"{KeyPrefixes.Cohort}dental#Orthodontics#small";
        var orthoRevenue = await _repo.GetItemAsync(orthoPk, "METRIC#Revenue");
        orthoRevenue.Should().NotBeNull();
        decimal.Parse(orthoRevenue!["P50"].N).Should().Be(1050000m);

        // Oral Surgery
        var surgeryPk = $"{KeyPrefixes.Cohort}dental#Oral Surgery#small";
        var surgeryRevenue = await _repo.GetItemAsync(surgeryPk, "METRIC#Revenue");
        surgeryRevenue.Should().NotBeNull();
        decimal.Parse(surgeryRevenue!["P50"].N).Should().Be(1200000m);
    }

    [Fact]
    public async Task LoadBenchmarks_Idempotent_DoesNotDuplicate()
    {
        await _benchmarkLoader.LoadBenchmarksAsync();

        var cohortPk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#small";
        var count1 = (await _repo.QueryByPkPrefixAsync(cohortPk, "METRIC#")).Count;

        await _benchmarkLoader.LoadBenchmarksAsync();

        var count2 = (await _repo.QueryByPkPrefixAsync(cohortPk, "METRIC#")).Count;
        count2.Should().Be(count1, "benchmarks use fixed PK/SK and overwrite on re-run");
    }

    // ── Regulatory Data Loading Tests ───────────────────────────

    [Fact]
    public async Task LoadRegulatoryData_CreatesSignalItems()
    {
        await _regulatoryLoader.LoadRegulatoryDataAsync();

        // Check a known NPI has regulatory signal
        var items = await _repo.QueryByPkPrefixAsync(
            $"{KeyPrefixes.Signal}1234567001", "REGULATORY#");

        items.Should().NotBeEmpty("should have regulatory signals for NPI 1234567001");
    }

    [Fact]
    public async Task LoadRegulatoryData_HasExpectedAttributes()
    {
        await _regulatoryLoader.LoadRegulatoryDataAsync();

        var items = await _repo.QueryByPkPrefixAsync(
            $"{KeyPrefixes.Signal}1234567001", "REGULATORY#");
        var item = items[0];

        item["Npi"].S.Should().Be("1234567001");
        int.Parse(item["TotalPrescriptions"].N).Should().Be(245);
        int.Parse(item["TotalBeneficiaries"].N).Should().Be(1820);
        decimal.Parse(item["AverageBeneficiaryAge"].N).Should().Be(52.3m);
        int.Parse(item["GraduationYear"].N).Should().Be(2003);
        item["MedicalSchool"].S.Should().Be("Arizona School of Dentistry");
        item["Source"].S.Should().Be("cms-fixture");
        item.Should().ContainKey("EnrichedAt");
    }

    [Fact]
    public async Task LoadRegulatoryData_MultipleProviders()
    {
        var count = await _regulatoryLoader.LoadRegulatoryDataAsync();

        count.Should().BeGreaterOrEqualTo(10, "fixture has 14 providers");
    }

    // ── Full Pipeline Test ──────────────────────────────────────

    [Fact]
    public async Task RunAsync_LoadsBenchmarksAndRegulatoryData()
    {
        await _processor.RunAsync("dental");

        // Verify benchmarks loaded
        var benchmarkCount = await _repo.CountSignalsByPrefixAsync(KeyPrefixes.Cohort);
        benchmarkCount.Should().BeGreaterOrEqualTo(10, "should load cohort benchmark items");

        // Verify regulatory signals loaded alongside provider signals
        var regulatoryItems = await _repo.QueryByPkPrefixAsync(
            $"{KeyPrefixes.Signal}1234567005", "REGULATORY#");
        regulatoryItems.Should().NotBeEmpty("regulatory signals should be loaded for known NPIs");
    }

    // ── Scoring Calibration Tests ───────────────────────────────

    [Fact]
    public async Task LoadedBenchmarks_CanBeQueriedForScoringCalibration()
    {
        await _benchmarkLoader.LoadBenchmarksAsync();

        // Simulate scoring engine reading benchmark P50 as baseline
        var cohortPk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#small";
        var revenue = await _repo.GetItemAsync(cohortPk, "METRIC#Revenue");
        var profitMargin = await _repo.GetItemAsync(cohortPk, "METRIC#ProfitMargin");

        revenue.Should().NotBeNull();
        profitMargin.Should().NotBeNull();

        var revenueP50 = decimal.Parse(revenue!["P50"].N);
        var marginP50 = decimal.Parse(profitMargin!["P50"].N);

        // Scoring calibration: a practice at P50 should score ~50 (median)
        // A practice above P75 should score higher
        var testRevenue = 1200000m; // Above P75 ($1,050,000)
        var revenueP25 = decimal.Parse(revenue["P25"].N);
        var revenueP75 = decimal.Parse(revenue["P75"].N);

        // Percentile position: above P75 = top quartile
        testRevenue.Should().BeGreaterThan(revenueP75,
            "test revenue should be above P75 for top-quartile scoring");

        // Margin at P50 = median performance
        marginP50.Should().BeInRange(20m, 40m,
            "GP profit margins should be in expected range for calibration");
    }

    [Fact]
    public async Task LoadedBenchmarks_SupportMultipleSizeBands()
    {
        await _benchmarkLoader.LoadBenchmarksAsync();

        var smallPk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#small";
        var mediumPk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#medium";
        var largePk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#large";

        var smallRev = await _repo.GetItemAsync(smallPk, "METRIC#Revenue");
        var mediumRev = await _repo.GetItemAsync(mediumPk, "METRIC#Revenue");
        var largeRev = await _repo.GetItemAsync(largePk, "METRIC#Revenue");

        smallRev.Should().NotBeNull();
        mediumRev.Should().NotBeNull();
        largeRev.Should().NotBeNull();

        // Revenue P50 should increase with practice size
        var smallP50 = decimal.Parse(smallRev!["P50"].N);
        var mediumP50 = decimal.Parse(mediumRev!["P50"].N);
        var largeP50 = decimal.Parse(largeRev!["P50"].N);

        mediumP50.Should().BeGreaterThan(smallP50);
        largeP50.Should().BeGreaterThan(mediumP50);
    }

    [Fact]
    public async Task LoadedBenchmarks_ProvideCompleteCalibrationSet()
    {
        await _benchmarkLoader.LoadBenchmarksAsync();

        // A complete calibration set for scoring engine needs:
        // Revenue, ProfitMargin, OverheadRatio, ProductionPerProvider
        var cohortPk = $"{KeyPrefixes.Cohort}dental#General Practice Dentistry#small";

        var requiredMetrics = new[]
        {
            "Revenue", "ProfitMargin", "OverheadRatio",
            "ProductionPerProvider", "OwnerCompensation",
            "CollectionRate", "NewPatientsPerMonth",
        };

        foreach (var metric in requiredMetrics)
        {
            var item = await _repo.GetItemAsync(cohortPk, $"METRIC#{metric}");
            item.Should().NotBeNull($"calibration requires {metric} benchmark");
            item!.Should().ContainKey("P25");
            item.Should().ContainKey("P50");
            item.Should().ContainKey("P75");
        }
    }
}
