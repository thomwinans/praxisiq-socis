using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;
using Snapp.Service.Enrichment.Services;
using Snapp.Service.Intelligence.Config;
using Snapp.Service.Intelligence.Handlers;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Constants;
using Xunit;

namespace Snapp.Service.Enrichment.Tests;

public class StateLicensingIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly AmazonDynamoDBClient _client;
    private readonly EnrichmentRepository _repo;
    private readonly IntelligenceRepository _intelRepo;
    private readonly StateLicensingLoader _loader;
    private readonly FixtureProviderSource _providerSource;

    public StateLicensingIntegrationTests()
    {
        _client = new AmazonDynamoDBClient(
            "fakeAccessKey", "fakeSecretKey",
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8042" });

        _repo = new EnrichmentRepository(
            _client,
            NullLogger<EnrichmentRepository>.Instance);

        _intelRepo = new IntelligenceRepository(_client);

        _providerSource = new FixtureProviderSource(
            NullLogger<FixtureProviderSource>.Instance);

        var licensingSource = new FixtureStateLicensingSource(
            NullLogger<FixtureStateLicensingSource>.Instance);

        _loader = new StateLicensingLoader(
            licensingSource,
            _providerSource,
            _repo,
            NullLogger<StateLicensingLoader>.Instance);
    }

    public async Task InitializeAsync() => await _repo.EnsureTableAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task LoadAndMatchAsync_CreatesLicenseSignalItems()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        matches.Should().NotBeEmpty();
        matches.Count.Should().BeGreaterOrEqualTo(30, "most of 50 licenses should match providers by name+city");
    }

    [Fact]
    public async Task LoadAndMatchAsync_SignalItemsHaveExpectedAttributes()
    {
        await _loader.LoadAndMatchAsync("dental");

        // Query a known provider's license signal (James Mitchell, NPI 1234567001)
        var items = await _repo.QueryByPkPrefixAsync(
            $"{KeyPrefixes.Signal}1234567001", "LICENSE#");

        items.Should().NotBeEmpty();
        var item = items[0];

        item["Npi"].S.Should().Be("1234567001");
        item["LicenseNumber"].S.Should().Contain("AZ-DDS-2005");
        item["CredentialType"].S.Should().Be("DDS");
        item["LicenseStatus"].S.Should().Be("active");
        item["LicenseState"].S.Should().Be("AZ");
        item.Should().ContainKey("MatchConfidence");
        item.Should().ContainKey("TenureYearsFromLicense");
        item.Should().ContainKey("IssueDate");
        item.Should().ContainKey("GSI1PK");
        item.Should().ContainKey("GSI1SK");
    }

    [Fact]
    public async Task LoadAndMatchAsync_ComputesTenureYearsFromLicenseIssueDate()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        // James Mitchell license issued 2005-03-01 → ~21 years tenure
        var mitchell = matches.FirstOrDefault(m => m.Provider.Npi == "1234567001");
        mitchell.Should().NotBeNull();
        mitchell!.TenureYearsFromLicense.Should().BeGreaterThan(20m,
            "license issued in 2005 should yield 20+ years tenure");
    }

    [Fact]
    public async Task LoadAndMatchAsync_ExactNameCityMatch_HasHighConfidence()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        // Providers with exact name+city match should get 0.95 confidence
        var exactMatches = matches.Where(m => m.MatchMethod == "name-city-exact").ToList();
        exactMatches.Should().NotBeEmpty();
        exactMatches.Should().AllSatisfy(m =>
            m.MatchConfidence.Should().Be(0.95m));
    }

    [Fact]
    public async Task LoadAndMatchAsync_StoresSignalsInDynamoDB()
    {
        await _loader.LoadAndMatchAsync("dental");

        // Verify signals are queryable by prefix
        var signalCount = await _repo.CountSignalsByPrefixAsync(KeyPrefixes.Signal);
        signalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LoadAndMatchAsync_PartialMatchHasLowerConfidence()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        // Verify that non-exact matches exist and have lower confidence
        var nonExactMatches = matches.Where(m => m.MatchMethod != "name-city-exact").ToList();
        if (nonExactMatches.Count > 0)
        {
            nonExactMatches.Should().AllSatisfy(m =>
                m.MatchConfidence.Should().BeLessThan(0.95m,
                    "partial/fuzzy matches should have lower confidence than exact matches"));
        }
    }

    [Fact]
    public async Task GetLicenseTenureByNpi_ReturnsMaxTenure_AfterEnrichment()
    {
        await _loader.LoadAndMatchAsync("dental");

        // James Mitchell (NPI 1234567001) — license issued 2005-03-01
        var tenure = await _intelRepo.GetLicenseTenureByNpiAsync("1234567001");
        tenure.Should().BeGreaterThan(20m,
            "license issued 2005-03-01 should yield >20 years tenure");
    }

    [Fact]
    public async Task GetLicenseTenureByNpi_ReturnsZero_ForUnknownNpi()
    {
        await _loader.LoadAndMatchAsync("dental");

        var tenure = await _intelRepo.GetLicenseTenureByNpiAsync("9999999999");
        tenure.Should().Be(0m);
    }

    [Fact]
    public async Task CareerStageClassifier_UsesLicenseTenure_WhenExplicitTenureIsZero()
    {
        await _loader.LoadAndMatchAsync("dental");

        var licenseTenure = await _intelRepo.GetLicenseTenureByNpiAsync("1234567001");
        licenseTenure.Should().BeGreaterThan(0m);

        var config = LoadDentalVerticalConfig();
        var classifier = new CareerStageClassifier(config);

        // No explicit tenure, but license tenure says 20+ years
        var result = classifier.Classify(new CareerStageInput
        {
            TenureYears = 0,
            LicenseTenureYears = licenseTenure,
            ProviderCount = 1,
            LocationCount = 1,
            CoLocationCount = 0,
            EntityType = "solo",
            CeHoursRecent = 5,
            OwnerProductionPct = 85,
        });

        // With 20+ years license tenure + low CE hours + solo → should classify as PreExit
        result.Stage.Should().Be("PreExit",
            "20+ years license tenure with low CE hours should classify as PreExit");
        result.TriggerSignals.Should().Contain(s => s.StartsWith("licenseTenure="),
            "license tenure should appear in trigger signals");
    }

    [Fact]
    public async Task CareerStageClassifier_LicenseTenure_IncreasesConfidence()
    {
        var config = LoadDentalVerticalConfig();
        var classifier = new CareerStageClassifier(config);

        // Without license tenure
        var withoutLicense = classifier.Classify(new CareerStageInput
        {
            TenureYears = 15,
            ProviderCount = 2,
            LocationCount = 1,
            CoLocationCount = 3,
            ProductionVolume = 600000,
            EntityType = "group",
            OwnerProductionPct = 60,
            CeHoursRecent = 20,
        });

        // Same input WITH license tenure
        var withLicense = classifier.Classify(new CareerStageInput
        {
            TenureYears = 15,
            LicenseTenureYears = 15,
            ProviderCount = 2,
            LocationCount = 1,
            CoLocationCount = 3,
            ProductionVolume = 600000,
            EntityType = "group",
            OwnerProductionPct = 60,
            CeHoursRecent = 20,
        });

        // Both should classify the same stage, but license adds an extra signal for confidence
        withLicense.Stage.Should().Be(withoutLicense.Stage);

        // Confidence should be at least as high (extra signal = higher or equal)
        var confidenceOrder = new Dictionary<string, int> { ["low"] = 0, ["medium"] = 1, ["high"] = 2 };
        confidenceOrder[withLicense.ConfidenceLevel].Should()
            .BeGreaterThanOrEqualTo(confidenceOrder[withoutLicense.ConfidenceLevel],
                "license tenure signal should maintain or increase confidence");
    }

    [Fact]
    public async Task CareerStageClassifier_UsesMaxOfExplicitAndLicenseTenure()
    {
        var config = LoadDentalVerticalConfig();
        var classifier = new CareerStageClassifier(config);

        // Explicit tenure = 5 years, but license says 22 years (more authoritative)
        var result = classifier.Classify(new CareerStageInput
        {
            TenureYears = 5,
            LicenseTenureYears = 22,
            ProviderCount = 1,
            LocationCount = 1,
            CoLocationCount = 0,
            EntityType = "solo",
            CeHoursRecent = 8,
            OwnerProductionPct = 90,
        });

        // 22 years effective tenure + low CE hours + solo → PreExit
        result.Stage.Should().Be("PreExit",
            "classifier should use max(explicit=5, license=22) = 22 years for tenure");
        result.TriggerSignals.Should().Contain(s => s.StartsWith("licenseTenure="));
    }

    private static VerticalConfig LoadDentalVerticalConfig()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src",
            "Snapp.Service.Intelligence", "Config", "verticals", "dental.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "src",
                "Snapp.Service.Intelligence", "Config", "verticals", "dental.json");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<VerticalConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
