using Amazon.DynamoDBv2;
using Snapp.Service.Network.Endpoints;
using Snapp.Service.Network.Repositories;
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

// Repositories — register concrete type so endpoints can use application methods
// that aren't part of INetworkRepository
builder.Services.AddSingleton<NetworkRepository>();
builder.Services.AddSingleton<INetworkRepository>(sp => sp.GetRequiredService<NetworkRepository>());

// JSON structured logging
builder.Logging.AddJsonConsole(options =>
{
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Snapp.Service.Network" }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .ExcludeFromDescription();

app.MapNetworkEndpoints();
app.MapMembershipEndpoints();

#if LAMBDA
await app.RunLambdaAsync();
#else
app.Run();
#endif
