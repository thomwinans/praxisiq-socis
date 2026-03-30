using Amazon.DynamoDBv2;
using Snapp.Service.Content.Endpoints;
using Snapp.Service.Content.Repositories;
using Snapp.Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

// DynamoDB
var dynamoConfig = new AmazonDynamoDBConfig();
var serviceUrl = builder.Configuration["DynamoDB:ServiceURL"];
if (!string.IsNullOrEmpty(serviceUrl))
    dynamoConfig.ServiceURL = serviceUrl;
builder.Services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(dynamoConfig));

// Repositories — register concrete type so endpoints can use extended methods
builder.Services.AddSingleton<ContentRepository>();
builder.Services.AddSingleton<IContentRepository>(sp => sp.GetRequiredService<ContentRepository>());

// JSON structured logging
builder.Logging.AddJsonConsole(options =>
{
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Snapp.Service.Content" }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .ExcludeFromDescription();

app.MapDiscussionEndpoints();
app.MapFeedEndpoints();

#if LAMBDA
await app.RunLambdaAsync();
#else
app.Run();
#endif
