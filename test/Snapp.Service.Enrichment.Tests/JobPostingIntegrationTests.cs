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

public class JobPostingIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly AmazonDynamoDBClient _client;
    private readonly EnrichmentRepository _repo;
    private readonly JobPostingLoader _loader;

    public JobPostingIntegrationTests()
    {
        _client = new AmazonDynamoDBClient(
            "fakeAccessKey", "fakeSecretKey",
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8042" });

        _repo = new EnrichmentRepository(
            _client,
            NullLogger<EnrichmentRepository>.Instance);

        var jobPostingSource = new FixtureJobPostingSource(
            NullLogger<FixtureJobPostingSource>.Instance);

        _loader = new JobPostingLoader(
            jobPostingSource,
            _repo,
            NullLogger<JobPostingLoader>.Instance);
    }

    public async Task InitializeAsync() => await _repo.EnsureTableAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task LoadAndAnalyzeAsync_ReturnsPracticeAnalyses()
    {
        var analyses = await _loader.LoadAndAnalyzeAsync();

        analyses.Should().NotBeEmpty();
        analyses.Count.Should().BeGreaterOrEqualTo(15, "fixture contains postings for 20 practices");
    }

    [Fact]
    public async Task LoadAndAnalyzeAsync_DetectsChronicTurnover()
    {
        var analyses = await _loader.LoadAndAnalyzeAsync();

        // Mitchell Family Dentistry has 3x Dental Hygienist postings → chronic turnover
        var mitchell = analyses.FirstOrDefault(a =>
            a.PracticeName == "Mitchell Family Dentistry");
        mitchell.Should().NotBeNull();
        mitchell!.ChronicTurnoverSignal.Should().BeTrue(
            "3 postings for Dental Hygienist exceeds chronic turnover threshold");

        var hygienistRole = mitchell.RoleRepetitions
            .FirstOrDefault(r => r.Role == "Dental Hygienist");
        hygienistRole.Should().NotBeNull();
        hygienistRole!.Count.Should().BeGreaterOrEqualTo(3);
        hygienistRole.IsChronicTurnover.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAndAnalyzeAsync_ComputesWorkforcePressureScore()
    {
        var analyses = await _loader.LoadAndAnalyzeAsync();

        // Anderson Dental Group: 4 postings, 3 urgent, chronic turnover → high pressure
        var anderson = analyses.FirstOrDefault(a =>
            a.PracticeName == "Anderson Dental Group");
        anderson.Should().NotBeNull();
        anderson!.WorkforcePressureScore.Should().BeGreaterThan(50m,
            "practice with chronic turnover and urgency should have high pressure score");

        // Dallas Premier Dental: 1 posting, no urgency → low pressure
        var dallas = analyses.FirstOrDefault(a =>
            a.PracticeName == "Dallas Premier Dental");
        dallas.Should().NotBeNull();
        dallas!.WorkforcePressureScore.Should().BeLessThan(40m,
            "single non-urgent posting should have low pressure score");
    }

    [Fact]
    public async Task LoadAndAnalyzeAsync_CountsUrgentPostings()
    {
        var analyses = await _loader.LoadAndAnalyzeAsync();

        // Manhattan Dental Group: 2 urgent out of 3
        var manhattan = analyses.FirstOrDefault(a =>
            a.PracticeName == "Manhattan Dental Group");
        manhattan.Should().NotBeNull();
        manhattan!.UrgentPostings.Should().Be(2);
        manhattan.TotalPostings.Should().Be(3);
    }

    [Fact]
    public async Task LoadAndAnalyzeAsync_StoresAnalysesInDynamoDB()
    {
        var analyses = await _loader.LoadAndAnalyzeAsync();

        // Verify by querying a known practice
        var pk = "JOBPOST#Mitchell Family Dentistry|Phoenix|AZ";
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new(pk),
                [":prefix"] = new("ANALYSIS#"),
            },
        });

        response.Items.Should().NotBeEmpty("Mitchell Family Dentistry should have an analysis item");
        analyses.Count.Should().BeGreaterOrEqualTo(15,
            "fixture contains postings for 20 practices");
    }

    [Fact]
    public async Task LoadAndAnalyzeAsync_AnalysisItemsHaveExpectedAttributes()
    {
        await _loader.LoadAndAnalyzeAsync();

        // Query Mitchell Family Dentistry directly by PK
        var pk = "JOBPOST#Mitchell Family Dentistry|Phoenix|AZ";
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new(pk),
                [":prefix"] = new("ANALYSIS#"),
            },
        });

        response.Items.Should().NotBeEmpty();
        var item = response.Items[0];

        item["PracticeName"].S.Should().Be("Mitchell Family Dentistry");
        item["PracticeCity"].S.Should().Be("Phoenix");
        item["PracticeState"].S.Should().Be("AZ");
        item.Should().ContainKey("TotalPostings");
        item.Should().ContainKey("WorkforcePressureScore");
        item.Should().ContainKey("ChronicTurnoverSignal");
        item.Should().ContainKey("RoleRepetitions");
        item.Should().ContainKey("GSI1PK");
    }

    [Fact]
    public async Task LoadAndAnalyzeAsync_ComputesPostingFrequency()
    {
        var analyses = await _loader.LoadAndAnalyzeAsync();

        // Practices with multiple postings over time should have measurable frequency
        var multiPosting = analyses.Where(a => a.TotalPostings >= 3).ToList();
        multiPosting.Should().NotBeEmpty();
        multiPosting.Should().AllSatisfy(a =>
            a.PostingFrequency.Should().BeGreaterThan(0m,
                "practices with multiple postings should have non-zero frequency"));
    }
}
