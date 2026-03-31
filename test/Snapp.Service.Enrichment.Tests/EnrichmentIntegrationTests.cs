using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;
using Snapp.Service.Enrichment.Services;
using Snapp.Shared.Constants;
using Snapp.TestHelpers;
using Xunit;

namespace Snapp.Service.Enrichment.Tests;

public class EnrichmentIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly AmazonDynamoDBClient _client;
    private readonly EnrichmentRepository _repo;
    private readonly EnrichmentProcessor _processor;

    public EnrichmentIntegrationTests()
    {
        _client = new AmazonDynamoDBClient(
            "fakeAccessKey", "fakeSecretKey",
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8042" });

        _repo = new EnrichmentRepository(
            _client,
            NullLogger<EnrichmentRepository>.Instance);

        var fixtureProviderSource = new FixtureProviderSource(
            NullLogger<FixtureProviderSource>.Instance);

        var fixtureMarketSource = new FixtureMarketSource(
            NullLogger<FixtureMarketSource>.Instance);

        var benchmarkLoader = new BenchmarkDataLoader(
            _repo,
            NullLogger<BenchmarkDataLoader>.Instance);

        var regulatoryLoader = new RegulatoryDataLoader(
            _repo,
            NullLogger<RegulatoryDataLoader>.Instance);

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
            fixtureLicensingSource,
            fixtureProviderSource,
            _repo,
            NullLogger<StateLicensingLoader>.Instance);

        var fixtureJobPostingSource = new FixtureJobPostingSource(
            NullLogger<FixtureJobPostingSource>.Instance);

        var jobPostingLoader = new JobPostingLoader(
            fixtureJobPostingSource,
            _repo,
            NullLogger<JobPostingLoader>.Instance);

        _processor = new EnrichmentProcessor(
            fixtureProviderSource,
            fixtureMarketSource,
            _repo,
            benchmarkLoader,
            regulatoryLoader,
            businessListingLoader,
            licensingLoader,
            jobPostingLoader,
            NullLogger<EnrichmentProcessor>.Instance);
    }

    public async Task InitializeAsync()
    {
        // Ensure table exists (do NOT delete — other test assemblies share this table)
        await _repo.EnsureTableAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _client.Dispose();

    // ── Provider Signal Tests ───────────────────────────────────

    [Fact]
    public async Task RunAsync_CreatesProviderSignalItems()
    {
        await _processor.RunAsync("dental");

        var signalCount = await _repo.CountSignalsByPrefixAsync(KeyPrefixes.Signal);
        signalCount.Should().BeGreaterOrEqualTo(50, "fixture contains 75 providers");
    }

    [Fact]
    public async Task RunAsync_SignalItemsHaveExpectedAttributes()
    {
        await _processor.RunAsync("dental");

        // Query a known provider's signal (filter for PROVIDER# SK prefix)
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Signal}1234567001"),
                [":skPrefix"] = new("PROVIDER#"),
            },
        });

        response.Items.Should().NotBeEmpty();
        var item = response.Items[0];

        item["Npi"].S.Should().Be("1234567001");
        item["FirstName"].S.Should().Be("James");
        item["LastName"].S.Should().Be("Mitchell");
        item["Specialty"].S.Should().Be("General Practice Dentistry");
        item["State"].S.Should().Be("AZ");
        item["Source"].S.Should().Be("nppes");
        item.Should().ContainKey("ConfidenceScore");
        item.Should().ContainKey("EnrichedAt");
        item.Should().ContainKey("GSI1PK");
        item.Should().ContainKey("GSI1SK");
    }

    [Fact]
    public async Task RunAsync_SignalConfidenceScore_ReflectsDataCompleteness()
    {
        await _processor.RunAsync("dental");

        // Provider with email should have higher confidence than one without
        var withEmail = await QueryFirstSignal("1234567001"); // has email
        var withoutEmail = await QueryFirstSignal("1234567003"); // no email

        var confWithEmail = decimal.Parse(withEmail!["ConfidenceScore"].N);
        var confWithoutEmail = decimal.Parse(withoutEmail!["ConfidenceScore"].N);

        confWithEmail.Should().BeGreaterThan(confWithoutEmail,
            "providers with email should get a higher confidence score");
    }

    // ── Market Data Tests ───────────────────────────────────────

    [Fact]
    public async Task RunAsync_CreatesMarketProfileItems()
    {
        await _processor.RunAsync("dental");

        var mktCount = await _repo.CountSignalsByPrefixAsync(KeyPrefixes.Market);
        mktCount.Should().BeGreaterOrEqualTo(15, "fixture contains 15 counties");
    }

    [Fact]
    public async Task RunAsync_MarketProfileHasExpectedStructure()
    {
        await _processor.RunAsync("dental");

        // Check Maricopa County profile
        var profile = await _repo.GetItemAsync($"{KeyPrefixes.Market}04013", "PROFILE");
        profile.Should().NotBeNull();
        profile!["GeoName"].S.Should().Contain("Maricopa");
        profile.Should().ContainKey("PractitionerDensity");
        profile.Should().ContainKey("CompetitorCount");
        profile.Should().ContainKey("ConsolidationPressure");

        // Check demographic sub-items
        var population = await _repo.GetItemAsync($"{KeyPrefixes.Market}04013", "DEMO#Population");
        population.Should().NotBeNull();
        decimal.Parse(population!["Value"].N).Should().BeGreaterThan(1000000);

        // Check workforce sub-items
        var providerCount = await _repo.GetItemAsync($"{KeyPrefixes.Market}04013", "WORKFORCE#DentalProviderCount");
        providerCount.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_MarketProfiles_ContainDemographicAndWorkforceData()
    {
        await _processor.RunAsync("dental");

        // Query all items under a single market
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Market}04013"),
            },
        });

        var sortKeys = response.Items.Select(i => i["SK"].S).ToList();

        // Expect: PROFILE + 6 DEMO# + 3 WORKFORCE# = 10 items
        sortKeys.Should().Contain("PROFILE");
        sortKeys.Should().Contain(sk => sk.StartsWith("DEMO#"));
        sortKeys.Should().Contain(sk => sk.StartsWith("WORKFORCE#"));
        response.Items.Count.Should().BeGreaterOrEqualTo(10);
    }

    // ── Idempotency Test ────────────────────────────────────────

    [Fact]
    public async Task RunAsync_Twice_DoesNotDuplicateMarketProfiles()
    {
        await _processor.RunAsync("dental");

        // Count market profile items for one county
        var count1 = await CountMarketItems("04013");

        // Run again
        await _processor.RunAsync("dental");

        var count2 = await CountMarketItems("04013");

        // Market profiles should be overwritten, not duplicated
        count2.Should().Be(count1, "market profiles use fixed PK/SK and overwrite on re-run");
    }

    // ── Edge Cases ──────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_UnknownVertical_CompletesWithoutError()
    {
        // Run once first to establish baseline (including regulatory signals)
        await _processor.RunAsync("dental");
        var beforeCount = await _repo.CountSignalsByPrefixAsync(KeyPrefixes.Signal);

        // Should not throw, just logs a warning about no taxonomy codes
        // Regulatory signals use ULIDs so they add new items each run
        await _processor.RunAsync("veterinary");

        // No new PROVIDER signals created (no matching taxonomy codes)
        // but regulatory signals may add new items (ULID-keyed)
        var afterCount = await _repo.CountSignalsByPrefixAsync(KeyPrefixes.Signal);
        afterCount.Should().BeGreaterOrEqualTo(beforeCount);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private async Task<Dictionary<string, AttributeValue>?> QueryFirstSignal(string npi)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Signal}{npi}"),
                [":skPrefix"] = new("PROVIDER#"),
            },
            Limit = 1,
        });
        return response.Items.Count > 0 ? response.Items[0] : null;
    }

    private async Task<int> CountMarketItems(string countyFips)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new($"{KeyPrefixes.Market}{countyFips}"),
            },
            Select = Select.COUNT,
        });
        return response.Count;
    }
}
