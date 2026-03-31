using Amazon.DynamoDBv2;
using Snapp.Service.Transaction.Endpoints;
using Snapp.Service.Transaction.Repositories;
using Snapp.Service.Transaction.Services;
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

// Repositories
builder.Services.AddSingleton<TransactionRepository>();
builder.Services.AddSingleton<ITransactionRepository>(sp => sp.GetRequiredService<TransactionRepository>());
builder.Services.AddSingleton<INetworkRepository, NetworkReadRepository>();

// Services
builder.Services.AddSingleton<ReputationComputeHandler>();

// JSON structured logging
builder.Logging.AddJsonConsole(options =>
{
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Snapp.Service.Transaction" }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .ExcludeFromDescription();

app.MapReferralEndpoints();
app.MapReputationEndpoints();

#if LAMBDA
await app.RunLambdaAsync();
#else
app.Run();
#endif
