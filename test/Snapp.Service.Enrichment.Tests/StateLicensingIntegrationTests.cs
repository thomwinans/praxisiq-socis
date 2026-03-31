using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Snapp.Service.Enrichment.Models;
using Snapp.Service.Enrichment.Repositories;
using Snapp.Service.Enrichment.Services;
using Snapp.Shared.Constants;
using Xunit;

namespace Snapp.Service.Enrichment.Tests;

public class StateLicensingIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly AmazonDynamoDBClient _client;
    private readonly EnrichmentRepository _repo;
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
}
