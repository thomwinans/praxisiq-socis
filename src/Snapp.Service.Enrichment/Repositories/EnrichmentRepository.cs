using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Models;
using Snapp.Shared.Constants;

namespace Snapp.Service.Enrichment.Repositories;

public class EnrichmentRepository
{
    private readonly IAmazonDynamoDB _db;
    private readonly ILogger<EnrichmentRepository> _logger;

    public EnrichmentRepository(IAmazonDynamoDB db, ILogger<EnrichmentRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task EnsureTableAsync()
    {
        try
        {
            await _db.DescribeTableAsync(TableNames.Intelligence);
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogInformation("Creating {Table} table", TableNames.Intelligence);
            await _db.CreateTableAsync(new CreateTableRequest
            {
                TableName = TableNames.Intelligence,
                KeySchema =
                [
                    new KeySchemaElement("PK", KeyType.HASH),
                    new KeySchemaElement("SK", KeyType.RANGE),
                ],
                AttributeDefinitions =
                [
                    new AttributeDefinition("PK", ScalarAttributeType.S),
                    new AttributeDefinition("SK", ScalarAttributeType.S),
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            });
        }
    }

    public async Task SaveProviderSignalAsync(ProviderRecord provider, decimal confidenceScore)
    {
        var now = DateTime.UtcNow.ToString("O");
        var signalId = Ulid.NewUlid().ToString();

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Signal}{provider.Npi}"),
            ["SK"] = new($"PROVIDER#{signalId}"),
            ["SignalId"] = new(signalId),
            ["Npi"] = new(provider.Npi),
            ["FirstName"] = new(provider.FirstName),
            ["LastName"] = new(provider.LastName),
            ["Credential"] = new(provider.Credential),
            ["Specialty"] = new(provider.Specialty),
            ["TaxonomyCode"] = new(provider.TaxonomyCode),
            ["PracticeAddress"] = new(provider.PracticeAddress),
            ["City"] = new(provider.City),
            ["State"] = new(provider.State),
            ["ZipCode"] = new(provider.ZipCode),
            ["EnumerationDate"] = new(provider.EnumerationDate),
            ["CoLocatedProviderCount"] = new() { N = provider.CoLocatedProviderCount.ToString() },
            ["EntityType"] = new(provider.EntityType),
            ["ConfidenceScore"] = new() { N = confidenceScore.ToString("F2") },
            ["Source"] = new("nppes"),
            ["EnrichedAt"] = new(now),
        };

        if (!string.IsNullOrEmpty(provider.CountyFips))
            item["CountyFips"] = new(provider.CountyFips);
        if (!string.IsNullOrEmpty(provider.OrganizationName))
            item["OrganizationName"] = new(provider.OrganizationName);

        // GSI for lookups by state
        item["GSI1PK"] = new($"STATE#{provider.State}");
        item["GSI1SK"] = new($"NPI#{provider.Npi}");

        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = TableNames.Intelligence,
            Item = item,
        });
    }

    public async Task SaveProviderSignalsBatchAsync(List<(ProviderRecord Provider, decimal Confidence)> batch)
    {
        // DynamoDB BatchWriteItem supports up to 25 items per request
        var chunks = batch.Chunk(25);
        foreach (var chunk in chunks)
        {
            var requests = new List<WriteRequest>();
            foreach (var (provider, confidence) in chunk)
            {
                var now = DateTime.UtcNow.ToString("O");
                var signalId = Ulid.NewUlid().ToString();

                var item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"{KeyPrefixes.Signal}{provider.Npi}"),
                    ["SK"] = new($"PROVIDER#{signalId}"),
                    ["SignalId"] = new(signalId),
                    ["Npi"] = new(provider.Npi),
                    ["FirstName"] = new(provider.FirstName),
                    ["LastName"] = new(provider.LastName),
                    ["Credential"] = new(provider.Credential),
                    ["Specialty"] = new(provider.Specialty),
                    ["TaxonomyCode"] = new(provider.TaxonomyCode),
                    ["PracticeAddress"] = new(provider.PracticeAddress),
                    ["City"] = new(provider.City),
                    ["State"] = new(provider.State),
                    ["ZipCode"] = new(provider.ZipCode),
                    ["EnumerationDate"] = new(provider.EnumerationDate),
                    ["CoLocatedProviderCount"] = new() { N = provider.CoLocatedProviderCount.ToString() },
                    ["EntityType"] = new(provider.EntityType),
                    ["ConfidenceScore"] = new() { N = confidence.ToString("F2") },
                    ["Source"] = new("nppes"),
                    ["EnrichedAt"] = new(now),
                    ["GSI1PK"] = new($"STATE#{provider.State}"),
                    ["GSI1SK"] = new($"NPI#{provider.Npi}"),
                };

                if (!string.IsNullOrEmpty(provider.CountyFips))
                    item["CountyFips"] = new(provider.CountyFips);
                if (!string.IsNullOrEmpty(provider.OrganizationName))
                    item["OrganizationName"] = new(provider.OrganizationName);

                requests.Add(new WriteRequest { PutRequest = new PutRequest { Item = item } });
            }

            await _db.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [TableNames.Intelligence] = requests,
                },
            });
        }
    }

    public async Task SaveMarketProfileAsync(MarketRecord market)
    {
        var now = DateTime.UtcNow.ToString("O");
        var geoId = market.CountyFips;

        // Main profile item
        var profileItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"{KeyPrefixes.Market}{geoId}"),
            ["SK"] = new("PROFILE"),
            ["GeoId"] = new(geoId),
            ["GeoName"] = new($"{market.CountyName}, {market.State}"),
            ["PractitionerDensity"] = new() { N = market.ProvidersPer100K.ToString("F2") },
            ["CompetitorCount"] = new() { N = market.DentalProviderCount.ToString() },
            ["ConsolidationPressure"] = new() { N = ComputeConsolidationPressure(market).ToString("F2") },
            ["ComputedAt"] = new(now),
            ["Source"] = new("census-fixture"),
        };
        await _db.PutItemAsync(new PutItemRequest { TableName = TableNames.Intelligence, Item = profileItem });

        // Demographic items
        var demos = new List<(string Name, decimal Value, string Unit, string Direction)>
        {
            ("Population", market.Population, "count", market.PopulationGrowthRate > 0 ? "up" : market.PopulationGrowthRate < 0 ? "down" : "flat"),
            ("MedianHouseholdIncome", market.MedianHouseholdIncome, "USD", "flat"),
            ("MedianAge", market.MedianAge, "years", "flat"),
            ("PopulationGrowthRate", market.PopulationGrowthRate, "%", market.PopulationGrowthRate > 0 ? "up" : "down"),
            ("MedianHomeValue", market.MedianHomeValue, "USD", "flat"),
            ("UninsuredRate", market.UninsuredRate, "%", "flat"),
        };

        foreach (var (name, value, unit, direction) in demos)
        {
            var demoItem = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Market}{geoId}"),
                ["SK"] = new($"DEMO#{name}"),
                ["Name"] = new(name),
                ["Value"] = new() { N = value.ToString("F2") },
                ["Unit"] = new(unit),
                ["Direction"] = new(direction),
            };
            await _db.PutItemAsync(new PutItemRequest { TableName = TableNames.Intelligence, Item = demoItem });
        }

        // Workforce items
        var workforce = new List<(string Name, decimal Value, string Unit)>
        {
            ("DentalProviderCount", market.DentalProviderCount, "count"),
            ("ProvidersPer100K", market.ProvidersPer100K, "ratio"),
            ("DsoLocationCount", market.DsoLocationCount, "count"),
        };

        foreach (var (name, value, unit) in workforce)
        {
            var wfItem = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new($"{KeyPrefixes.Market}{geoId}"),
                ["SK"] = new($"WORKFORCE#{name}"),
                ["Name"] = new(name),
                ["Value"] = new() { N = value.ToString("F2") },
                ["Unit"] = new(unit),
            };
            await _db.PutItemAsync(new PutItemRequest { TableName = TableNames.Intelligence, Item = wfItem });
        }
    }

    public async Task<int> CountSignalsByPrefixAsync(string pkPrefix)
    {
        var response = await _db.ScanAsync(new ScanRequest
        {
            TableName = TableNames.Intelligence,
            FilterExpression = "begins_with(PK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":prefix"] = new(pkPrefix),
            },
            Select = Select.COUNT,
        });
        return response.Count;
    }

    public async Task<Dictionary<string, AttributeValue>?> GetItemAsync(string pk, string sk)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = TableNames.Intelligence,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new(pk),
                ["SK"] = new(sk),
            },
        });
        return response.IsItemSet ? response.Item : null;
    }

    private static decimal ComputeConsolidationPressure(MarketRecord market)
    {
        // Heuristic: higher DSO presence + lower provider density = higher pressure
        var dsoPressure = market.DsoLocationCount > 0
            ? Math.Min(1.0m, market.DsoLocationCount / 10.0m)
            : 0m;
        var densityFactor = market.ProvidersPer100K > 0
            ? Math.Min(1.0m, 60m / market.ProvidersPer100K)
            : 0.5m;

        return Math.Round((dsoPressure * 0.6m + densityFactor * 0.4m) * 100m, 2);
    }
}
