using Amazon.DynamoDBv2;
using Amazon.S3;
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

// S3 / MinIO
var s3Config = new AmazonS3Config { ForcePathStyle = true };
var s3ServiceUrl = builder.Configuration["S3:ServiceURL"];
if (!string.IsNullOrEmpty(s3ServiceUrl))
    s3Config.ServiceURL = s3ServiceUrl;
builder.Services.AddSingleton<IAmazonS3>(new AmazonS3Client(s3Config));

// Repositories
builder.Services.AddSingleton<TransactionRepository>();
builder.Services.AddSingleton<ITransactionRepository>(sp => sp.GetRequiredService<TransactionRepository>());
builder.Services.AddSingleton<IDealRoomRepository, DealRoomRepository>();
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
app.MapDealRoomEndpoints();

#if LAMBDA
await app.RunLambdaAsync();
#else
app.Run();
#endif
