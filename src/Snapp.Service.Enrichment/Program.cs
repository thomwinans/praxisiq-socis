using Amazon.DynamoDBv2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snapp.Service.Enrichment.Repositories;
using Snapp.Service.Enrichment.Services;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var services = new ServiceCollection();

// Logging
services.AddLogging(builder =>
{
    builder.AddJsonConsole(options =>
    {
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
});

// DynamoDB
var dynamoConfig = new AmazonDynamoDBConfig();
var serviceUrl = config["DynamoDB:ServiceURL"];
if (!string.IsNullOrEmpty(serviceUrl))
    dynamoConfig.ServiceURL = serviceUrl;
services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(dynamoConfig));

// Repository
services.AddSingleton<EnrichmentRepository>();

// Provider source — fixture or API based on config
var source = config["Enrichment:Source"] ?? "fixture";
if (source.Equals("api", StringComparison.OrdinalIgnoreCase))
{
    services.AddHttpClient();
    services.AddSingleton<IProviderSource, NppesProviderSource>();
}
else
{
    services.AddSingleton<IProviderSource, FixtureProviderSource>();
}

// Market data source (always fixture for now)
services.AddSingleton<IMarketSource, FixtureMarketSource>();

// Business listing source — fixture or API based on config
var listingSource = config["Enrichment:ListingSource"] ?? "fixture";
if (listingSource.Equals("api", StringComparison.OrdinalIgnoreCase))
{
    services.AddSingleton<IBusinessListingProvider, GooglePlacesClient>();
}
else
{
    services.AddSingleton<IBusinessListingProvider, FixtureBusinessListingSource>();
}

// State licensing source — fixture or API based on config
services.AddSingleton<IStateLicensingSource, FixtureStateLicensingSource>();

// Job posting source — fixture or API based on config
services.AddSingleton<IJobPostingSource, FixtureJobPostingSource>();

// Benchmark, regulatory, business listing, licensing, and job posting data loaders
services.AddSingleton<BenchmarkDataLoader>();
services.AddSingleton<RegulatoryDataLoader>();
services.AddSingleton<BusinessListingLoader>();
services.AddSingleton<StateLicensingLoader>();
services.AddSingleton<JobPostingLoader>();

// Processor
services.AddSingleton<EnrichmentProcessor>();

var sp = services.BuildServiceProvider();
var processor = sp.GetRequiredService<EnrichmentProcessor>();
var logger = sp.GetRequiredService<ILogger<Program>>();

var vertical = config["Enrichment:Vertical"] ?? "dental";

logger.LogInformation("Enrichment job starting. Source: {Source}, Vertical: {Vertical}", source, vertical);

await processor.RunAsync(vertical);

logger.LogInformation("Enrichment job finished");
