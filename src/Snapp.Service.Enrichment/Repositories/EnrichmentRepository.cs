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
        var total = 0;
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var request = new ScanRequest
            {
                TableName = TableNames.Intelligence,
                FilterExpression = "begins_with(PK, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":prefix"] = new(pkPrefix),
                },
                Select = Select.COUNT,
            };

            if (lastKey != null)
                request.ExclusiveStartKey = lastKey;

            var response = await _db.ScanAsync(request);
            total += response.Count;
            lastKey = response.LastEvaluatedKey;
        } while (lastKey != null && lastKey.Count > 0);

        return total;
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

    public async Task SaveBenchmarksBatchAsync(List<BenchmarkRecord> records)
    {
        var now = DateTime.UtcNow.ToString("O");
        var itemsByKey = new Dictionary<string, Dictionary<string, AttributeValue>>();

        foreach (var record in records)
        {
            // National cohort: COHORT#{vertical}#{specialty}#{sizeBand}
            // Geographic (state/county): BENCH#{vertical}#{geo}#{level}
            var pk = record.GeographicLevel == "national"
                && !string.IsNullOrEmpty(record.Specialty) && !string.IsNullOrEmpty(record.SizeBand)
                    ? $"{KeyPrefixes.Cohort}{record.Vertical}#{record.Specialty}#{record.SizeBand}"
                    : $"{KeyPrefixes.Benchmark}{record.Vertical}#{record.Geography}#{record.GeographicLevel}";

            var sk = $"METRIC#{record.MetricName}";

            var item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new(pk),
                ["SK"] = new(sk),
                ["MetricName"] = new(record.MetricName),
                ["P25"] = new() { N = record.P25.ToString("F2") },
                ["P50"] = new() { N = record.P50.ToString("F2") },
                ["P75"] = new() { N = record.P75.ToString("F2") },
                ["SampleSize"] = new() { N = record.SampleSize.ToString() },
                ["ComputedAt"] = new(now),
                ["Geography"] = new(record.Geography),
                ["Vertical"] = new(record.Vertical),
                ["Source"] = new("association-benchmark"),
                ["GSI1PK"] = new(pk),
                ["GSI1SK"] = new(sk),
            };

            if (record.Mean.HasValue)
                item["Mean"] = new() { N = record.Mean.Value.ToString("F2") };
            if (!string.IsNullOrEmpty(record.Specialty))
                item["Specialty"] = new(record.Specialty);
            if (!string.IsNullOrEmpty(record.SizeBand))
                item["SizeBand"] = new(record.SizeBand);
            if (!string.IsNullOrEmpty(record.GeographicLevel))
                item["GeographicLevel"] = new(record.GeographicLevel);

            // Dedup by PK+SK (last-write-wins)
            itemsByKey[$"{pk}|{sk}"] = item;
        }

        foreach (var chunk in itemsByKey.Values.Chunk(25))
        {
            var requests = chunk
                .Select(item => new WriteRequest { PutRequest = new PutRequest { Item = item } })
                .ToList();

            await _db.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [TableNames.Intelligence] = requests,
                },
            });
        }
    }

    public async Task SaveRegulatorySignalsBatchAsync(List<RegulatoryRecord> records)
    {
        var chunks = records.Chunk(25);
        foreach (var chunk in chunks)
        {
            var requests = new List<WriteRequest>();
            foreach (var record in chunk)
            {
                var now = DateTime.UtcNow.ToString("O");
                var signalId = Ulid.NewUlid().ToString();

                var item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"{KeyPrefixes.Signal}{record.Npi}"),
                    ["SK"] = new($"REGULATORY#{signalId}"),
                    ["SignalId"] = new(signalId),
                    ["Npi"] = new(record.Npi),
                    ["TotalPrescriptions"] = new() { N = record.TotalPrescriptions.ToString() },
                    ["OpioidPrescriptions"] = new() { N = record.OpioidPrescriptions.ToString() },
                    ["AntibioticPrescriptions"] = new() { N = record.AntibioticPrescriptions.ToString() },
                    ["TotalBeneficiaries"] = new() { N = record.TotalBeneficiaries.ToString() },
                    ["AverageBeneficiaryAge"] = new() { N = record.AverageBeneficiaryAge.ToString("F1") },
                    ["FemaleBeneficiaryPct"] = new() { N = record.FemaleBeneficiaryPct.ToString("F1") },
                    ["DualEligiblePct"] = new() { N = record.DualEligiblePct.ToString("F1") },
                    ["AverageRiskScore"] = new() { N = record.AverageRiskScore.ToString("F2") },
                    ["TotalMedicarePayments"] = new() { N = record.TotalMedicarePayments.ToString("F2") },
                    ["GraduationYear"] = new() { N = record.GraduationYear.ToString() },
                    ["MedicalSchool"] = new(record.MedicalSchool),
                    ["Source"] = new(record.Source),
                    ["EnrichedAt"] = new(now),
                    ["GSI1PK"] = new($"REGSIGNAL#{record.Source}"),
                    ["GSI1SK"] = new($"NPI#{record.Npi}"),
                };

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

    public async Task SaveBusinessListingSignalsBatchAsync(List<Models.ListingMatchResult> matches)
    {
        var chunks = matches.Chunk(25);
        foreach (var chunk in chunks)
        {
            var requests = new List<WriteRequest>();
            foreach (var match in chunk)
            {
                var now = DateTime.UtcNow.ToString("O");
                var signalId = Ulid.NewUlid().ToString();

                var item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"{KeyPrefixes.Signal}{match.Provider.Npi}"),
                    ["SK"] = new($"LISTING#{signalId}"),
                    ["SignalId"] = new(signalId),
                    ["Npi"] = new(match.Provider.Npi),
                    ["PlaceId"] = new(match.Listing.PlaceId),
                    ["ListingName"] = new(match.Listing.Name),
                    ["ListingAddress"] = new(match.Listing.Address),
                    ["ListingCity"] = new(match.Listing.City),
                    ["ListingState"] = new(match.Listing.State),
                    ["Rating"] = new() { N = match.Listing.Rating.ToString("F1") },
                    ["ReviewCount"] = new() { N = match.Listing.ReviewCount.ToString() },
                    ["MatchMethod"] = new(match.MatchMethod),
                    ["MatchConfidence"] = new() { N = match.MatchConfidence.ToString("F2") },
                    ["StrongOnlineReputation"] = new() { BOOL = match.StrongOnlineReputation },
                    ["Source"] = new("google-places-fixture"),
                    ["EnrichedAt"] = new(now),
                    ["GSI1PK"] = new($"LISTING#{match.Listing.State}"),
                    ["GSI1SK"] = new($"NPI#{match.Provider.Npi}"),
                };

                if (!string.IsNullOrEmpty(match.Listing.WebsiteUrl))
                    item["WebsiteUrl"] = new(match.Listing.WebsiteUrl);
                if (!string.IsNullOrEmpty(match.Listing.Phone))
                    item["ListingPhone"] = new(match.Listing.Phone);
                if (!string.IsNullOrEmpty(match.Listing.Category))
                    item["ListingCategory"] = new(match.Listing.Category);

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

    public async Task<List<Dictionary<string, AttributeValue>>> QueryByPkPrefixAsync(string pk, string skPrefix)
    {
        var response = await _db.QueryAsync(new QueryRequest
        {
            TableName = TableNames.Intelligence,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new(pk),
                [":prefix"] = new(skPrefix),
            },
        });
        return response.Items;
    }

    public async Task SaveStateLicensingSignalsBatchAsync(List<LicensingMatchResult> matches)
    {
        var chunks = matches.Chunk(25);
        foreach (var chunk in chunks)
        {
            var requests = new List<WriteRequest>();
            foreach (var match in chunk)
            {
                var now = DateTime.UtcNow.ToString("O");
                var signalId = Ulid.NewUlid().ToString();

                var item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"{KeyPrefixes.Signal}{match.Provider.Npi}"),
                    ["SK"] = new($"LICENSE#{signalId}"),
                    ["SignalId"] = new(signalId),
                    ["Npi"] = new(match.Provider.Npi),
                    ["LicenseNumber"] = new(match.License.LicenseNumber),
                    ["CredentialType"] = new(match.License.CredentialType),
                    ["LicenseStatus"] = new(match.License.Status),
                    ["IssueDate"] = new(match.License.IssueDate),
                    ["ExpirationDate"] = new(match.License.ExpirationDate),
                    ["LicenseState"] = new(match.License.State),
                    ["BoardName"] = new(match.License.BoardName),
                    ["MatchMethod"] = new(match.MatchMethod),
                    ["MatchConfidence"] = new() { N = match.MatchConfidence.ToString("F2") },
                    ["TenureYearsFromLicense"] = new() { N = match.TenureYearsFromLicense.ToString("F1") },
                    ["Source"] = new("state-licensing-fixture"),
                    ["EnrichedAt"] = new(now),
                    ["GSI1PK"] = new($"LICENSE#{match.License.State}"),
                    ["GSI1SK"] = new($"NPI#{match.Provider.Npi}"),
                };

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

    public async Task SaveJobPostingSignalsBatchAsync(List<JobPostingAnalysis> analyses)
    {
        var chunks = analyses.Chunk(25);
        foreach (var chunk in chunks)
        {
            var requests = new List<WriteRequest>();
            foreach (var analysis in chunk)
            {
                var now = DateTime.UtcNow.ToString("O");
                var signalId = Ulid.NewUlid().ToString();
                var practiceKey = $"{analysis.PracticeName}|{analysis.PracticeCity}|{analysis.PracticeState}";

                var item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"JOBPOST#{practiceKey}"),
                    ["SK"] = new($"ANALYSIS#{signalId}"),
                    ["SignalId"] = new(signalId),
                    ["PracticeName"] = new(analysis.PracticeName),
                    ["PracticeCity"] = new(analysis.PracticeCity),
                    ["PracticeState"] = new(analysis.PracticeState),
                    ["TotalPostings"] = new() { N = analysis.TotalPostings.ToString() },
                    ["UniqueRoles"] = new() { N = analysis.UniqueRoles.ToString() },
                    ["UrgentPostings"] = new() { N = analysis.UrgentPostings.ToString() },
                    ["PostingFrequency"] = new() { N = analysis.PostingFrequency.ToString("F2") },
                    ["ChronicTurnoverSignal"] = new() { BOOL = analysis.ChronicTurnoverSignal },
                    ["WorkforcePressureScore"] = new() { N = analysis.WorkforcePressureScore.ToString("F2") },
                    ["Source"] = new("job-posting-fixture"),
                    ["EnrichedAt"] = new(now),
                    ["GSI1PK"] = new($"JOBPOST#{analysis.PracticeState}"),
                    ["GSI1SK"] = new($"PRACTICE#{analysis.PracticeName}"),
                };

                // Store role repetitions as a map list
                if (analysis.RoleRepetitions.Count > 0)
                {
                    item["RoleRepetitions"] = new()
                    {
                        L = analysis.RoleRepetitions.Select(r => new AttributeValue
                        {
                            M = new Dictionary<string, AttributeValue>
                            {
                                ["Role"] = new(r.Role),
                                ["Count"] = new() { N = r.Count.ToString() },
                                ["IsChronicTurnover"] = new() { BOOL = r.IsChronicTurnover },
                            },
                        }).ToList(),
                    };
                }

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
