using Amazon.DynamoDBv2;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Snapp.Service.Enrichment.Repositories;
using Snapp.Service.Enrichment.Services;
using Snapp.Shared.Constants;
using Xunit;

namespace Snapp.Service.Enrichment.Tests;

public class BusinessListingIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly AmazonDynamoDBClient _client;
    private readonly EnrichmentRepository _repo;
    private readonly BusinessListingLoader _loader;
    private readonly EnrichmentProcessor _processor;

    public BusinessListingIntegrationTests()
    {
        _client = new AmazonDynamoDBClient(
            "fakeAccessKey", "fakeSecretKey",
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8042" });

        _repo = new EnrichmentRepository(
            _client,
            NullLogger<EnrichmentRepository>.Instance);

        var fixtureProviderSource = new FixtureProviderSource(
            NullLogger<FixtureProviderSource>.Instance);

        var fixtureListingSource = new FixtureBusinessListingSource(
            NullLogger<FixtureBusinessListingSource>.Instance);

        _loader = new BusinessListingLoader(
            fixtureListingSource,
            fixtureProviderSource,
            _repo,
            NullLogger<BusinessListingLoader>.Instance);

        var fixtureMarketSource = new FixtureMarketSource(
            NullLogger<FixtureMarketSource>.Instance);

        var benchmarkLoader = new BenchmarkDataLoader(
            _repo,
            NullLogger<BenchmarkDataLoader>.Instance);

        var regulatoryLoader = new RegulatoryDataLoader(
            _repo,
            NullLogger<RegulatoryDataLoader>.Instance);

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
            benchmarkLoader,
            regulatoryLoader,
            _loader,
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

    // ── Fixture Loading Tests ──────────────────────────────────

    [Fact]
    public async Task LoadAndMatch_ReturnsMatches()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        matches.Should().NotBeEmpty("should match listings to providers");
        matches.Count.Should().BeGreaterOrEqualTo(50, "most of 75 listings should match");
    }

    [Fact]
    public async Task LoadAndMatch_AllThreeMatchPassesWork()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        matches.Should().Contain(m => m.MatchMethod == "phone-exact",
            "Pass 1 should produce phone-exact matches");
        matches.Should().Contain(m => m.MatchMethod == "address-exact",
            "Pass 2 should produce address-exact matches");
        matches.Should().Contain(m => m.MatchMethod == "name-city-fuzzy",
            "Pass 3 should produce name-city-fuzzy matches");
    }

    // ── Phone Exact Match (Pass 1) ─────────────────────────────

    [Fact]
    public async Task LoadAndMatch_PhoneExact_Confidence1()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        var phoneMatches = matches.Where(m => m.MatchMethod == "phone-exact").ToList();

        phoneMatches.Should().NotBeEmpty();
        phoneMatches.Should().AllSatisfy(m =>
            m.MatchConfidence.Should().Be(1.0m, "phone exact match should have confidence 1.0"));
    }

    [Fact]
    public async Task LoadAndMatch_PhoneExact_MatchesKnownProvider()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        // NPI 1234567001 (James Mitchell) has phone 6025550101
        // Listing ChIJ_phone_001 has phone (602) 555-0101
        var match = matches.FirstOrDefault(m =>
            m.Provider.Npi == "1234567001" && m.MatchMethod == "phone-exact");

        match.Should().NotBeNull("Mitchell should match by phone");
        match!.Listing.PlaceId.Should().Be("ChIJ_phone_001");
        match.MatchConfidence.Should().Be(1.0m);
    }

    // ── Address Exact Match (Pass 2) ────────────────────────────

    [Fact]
    public async Task LoadAndMatch_AddressExact_Confidence08()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        var addrMatches = matches.Where(m => m.MatchMethod == "address-exact").ToList();

        addrMatches.Should().NotBeEmpty();
        addrMatches.Should().AllSatisfy(m =>
            m.MatchConfidence.Should().Be(0.8m, "address exact match should have confidence 0.8"));
    }

    [Fact]
    public async Task LoadAndMatch_AddressExact_MatchesKnownProvider()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        // NPI 1234567003 (Robert Williams) at 300 Elm St, Mesa, AZ — no phone
        // Listing ChIJ_addr_003 at 300 Elm St, Mesa, AZ
        var match = matches.FirstOrDefault(m =>
            m.Provider.Npi == "1234567003" && m.MatchMethod == "address-exact");

        match.Should().NotBeNull("Williams should match by address");
        match!.Listing.PlaceId.Should().Be("ChIJ_addr_003");
        match.MatchConfidence.Should().Be(0.8m);
    }

    // ── Name+City Fuzzy Match (Pass 3) ──────────────────────────

    [Fact]
    public async Task LoadAndMatch_NameCityFuzzy_ConfidenceBetween05And07()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        var fuzzyMatches = matches.Where(m => m.MatchMethod == "name-city-fuzzy").ToList();

        fuzzyMatches.Should().NotBeEmpty();
        fuzzyMatches.Should().AllSatisfy(m =>
        {
            m.MatchConfidence.Should().BeGreaterOrEqualTo(0.5m);
            m.MatchConfidence.Should().BeLessOrEqualTo(0.7m);
        });
    }

    [Fact]
    public async Task LoadAndMatch_NameCityFuzzy_MatchesKnownProvider()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        // NPI 1234567006 (Jennifer Lee) in Gilbert, AZ — no phone, address differs
        // Listing ChIJ_name_006 "Jennifer Lee DMD" in Gilbert, AZ
        var match = matches.FirstOrDefault(m =>
            m.Provider.Npi == "1234567006" && m.MatchMethod == "name-city-fuzzy");

        match.Should().NotBeNull("Jennifer Lee should match by name+city");
        match!.MatchConfidence.Should().BeGreaterOrEqualTo(0.5m);
    }

    // ── Reputation Flag Tests ───────────────────────────────────

    [Fact]
    public async Task LoadAndMatch_StrongReputation_FlaggedCorrectly()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        // Listings with rating >= 4.5 AND reviewCount >= 100 should be flagged
        var reputationMatches = matches.Where(m => m.StrongOnlineReputation).ToList();

        reputationMatches.Should().NotBeEmpty("some providers should have strong online reputation");
        reputationMatches.Should().AllSatisfy(m =>
        {
            m.Listing.Rating.Should().BeGreaterOrEqualTo(4.5m);
            m.Listing.ReviewCount.Should().BeGreaterOrEqualTo(100);
        });
    }

    [Fact]
    public async Task LoadAndMatch_WeakReputation_NotFlagged()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        var noReputation = matches.Where(m => !m.StrongOnlineReputation).ToList();

        noReputation.Should().NotBeEmpty("some listings should not meet the strong reputation threshold");
        noReputation.Should().AllSatisfy(m =>
        {
            // Either rating < 4.5 OR reviewCount < 100
            var meetsThreshold = m.Listing.Rating >= 4.5m && m.Listing.ReviewCount >= 100;
            meetsThreshold.Should().BeFalse();
        });
    }

    [Fact]
    public async Task LoadAndMatch_KnownStrongReputation()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        // ChIJ_phone_001 (Mitchell) has rating 4.7, reviews 142 → strong
        var mitchell = matches.FirstOrDefault(m => m.Provider.Npi == "1234567001");
        mitchell.Should().NotBeNull();
        mitchell!.StrongOnlineReputation.Should().BeTrue(
            "rating 4.7 and 142 reviews should flag strong-online-reputation");
    }

    [Fact]
    public async Task LoadAndMatch_KnownWeakReputation()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        // ChIJ_phone_007 (Brown Endodontics) has rating 4.2, reviews 45 → not strong
        var brown = matches.FirstOrDefault(m => m.Provider.Npi == "1234567007");
        brown.Should().NotBeNull();
        brown!.StrongOnlineReputation.Should().BeFalse(
            "rating 4.2 and 45 reviews should not flag strong-online-reputation");
    }

    // ── DynamoDB Signal Storage Tests ───────────────────────────

    [Fact]
    public async Task LoadAndMatch_SavesListingSignalsToDynamo()
    {
        await _loader.LoadAndMatchAsync("dental");

        // Check a known matched provider has LISTING# signal
        var items = await _repo.QueryByPkPrefixAsync(
            $"{KeyPrefixes.Signal}1234567001", "LISTING#");

        items.Should().NotBeEmpty("should have LISTING# signal for matched provider");
    }

    [Fact]
    public async Task LoadAndMatch_ListingSignalHasExpectedAttributes()
    {
        await _loader.LoadAndMatchAsync("dental");

        var items = await _repo.QueryByPkPrefixAsync(
            $"{KeyPrefixes.Signal}1234567001", "LISTING#");
        var item = items[0];

        item["Npi"].S.Should().Be("1234567001");
        item["PlaceId"].S.Should().Be("ChIJ_phone_001");
        item["ListingName"].S.Should().Be("Mitchell Family Dentistry");
        item["ListingCity"].S.Should().Be("Phoenix");
        item["ListingState"].S.Should().Be("AZ");
        decimal.Parse(item["Rating"].N).Should().Be(4.7m);
        int.Parse(item["ReviewCount"].N).Should().Be(142);
        item["MatchMethod"].S.Should().Be("phone-exact");
        decimal.Parse(item["MatchConfidence"].N).Should().Be(1.0m);
        item["StrongOnlineReputation"].BOOL.Should().BeTrue();
        item["Source"].S.Should().Be("google-places-fixture");
        item.Should().ContainKey("EnrichedAt");
        item.Should().ContainKey("WebsiteUrl");
    }

    [Fact]
    public async Task LoadAndMatch_MultipleProvidersHaveListingSignals()
    {
        await _loader.LoadAndMatchAsync("dental");

        // Spot-check a few different providers
        var npis = new[] { "1234567001", "1234567003", "1234567011", "1234567031" };

        foreach (var npi in npis)
        {
            var items = await _repo.QueryByPkPrefixAsync(
                $"{KeyPrefixes.Signal}{npi}", "LISTING#");
            items.Should().NotBeEmpty($"NPI {npi} should have a LISTING# signal");
        }
    }

    // ── Full Pipeline Integration ───────────────────────────────

    [Fact]
    public async Task RunAsync_IncludesBusinessListings()
    {
        await _processor.RunAsync("dental");

        // Verify listing signals exist alongside provider signals
        var listingItems = await _repo.QueryByPkPrefixAsync(
            $"{KeyPrefixes.Signal}1234567001", "LISTING#");
        listingItems.Should().NotBeEmpty("full pipeline should include business listing enrichment");

        var providerItems = await _repo.QueryByPkPrefixAsync(
            $"{KeyPrefixes.Signal}1234567001", "PROVIDER#");
        providerItems.Should().NotBeEmpty("provider signals should still exist");
    }

    // ── No Duplicate Match Tests ────────────────────────────────

    [Fact]
    public async Task LoadAndMatch_NoProviderMatchedTwice()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        var npis = matches.Select(m => m.Provider.Npi).ToList();
        npis.Should().OnlyHaveUniqueItems("each provider should match at most one listing");
    }

    [Fact]
    public async Task LoadAndMatch_NoListingMatchedTwice()
    {
        var matches = await _loader.LoadAndMatchAsync("dental");

        var placeIds = matches.Select(m => m.Listing.PlaceId).ToList();
        placeIds.Should().OnlyHaveUniqueItems("each listing should match at most one provider");
    }
}
