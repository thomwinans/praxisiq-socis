using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Snapp.Service.Intelligence.Config;
using Snapp.Service.Intelligence.Endpoints;
using Snapp.Service.Intelligence.Handlers;
using Snapp.Service.Intelligence.Repositories;
using Snapp.Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

// JSON serialization — accept enum values as strings
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// DynamoDB
var dynamoConfig = new AmazonDynamoDBConfig();
var serviceUrl = builder.Configuration["DynamoDB:ServiceURL"];
if (!string.IsNullOrEmpty(serviceUrl))
    dynamoConfig.ServiceURL = serviceUrl;
builder.Services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(dynamoConfig));

// Repositories — register concrete type so endpoints can use extended methods
builder.Services.AddSingleton<IntelligenceRepository>();
builder.Services.AddSingleton<IIntelligenceRepository>(sp => sp.GetRequiredService<IntelligenceRepository>());

// Vertical Configuration — load dental default config
var configPath = Path.Combine(AppContext.BaseDirectory, "Config", "dental-default.json");
if (!File.Exists(configPath))
{
    // Fall back to source path for local dev
    configPath = Path.Combine(Directory.GetCurrentDirectory(), "Config", "dental-default.json");
}
var configJson = File.ReadAllText(configPath);
var verticalConfig = JsonSerializer.Deserialize<VerticalConfig>(configJson, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
}) ?? throw new InvalidOperationException("Failed to load vertical configuration.");
builder.Services.AddSingleton(verticalConfig);

// Scoring Engine
builder.Services.AddSingleton<ScoringEngine>();

// Career Stage Classifier
builder.Services.AddSingleton<CareerStageClassifier>();

// JSON structured logging
builder.Logging.AddJsonConsole(options =>
{
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Snapp.Service.Intelligence" }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .ExcludeFromDescription();

app.MapContributionEndpoints();
app.MapScoreEndpoints();
app.MapBenchmarkEndpoints();
app.MapDashboardEndpoints();
app.MapCareerStageEndpoints();

#if LAMBDA
await app.RunLambdaAsync();
#else
app.Run();
#endif
