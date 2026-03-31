using Amazon.DynamoDBv2;
using Snapp.Service.LinkedIn.Clients;
using Snapp.Service.LinkedIn.Endpoints;
using Snapp.Service.LinkedIn.Repositories;
using Snapp.Service.LinkedIn.Services;
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

// Encryption
builder.Services.AddSingleton<IFieldEncryptor>(sp =>
    new LocalFileEncryptor(sp.GetRequiredService<IConfiguration>()));

// LinkedIn API client (mock for dev)
builder.Services.AddSingleton<ILinkedInClient, MockLinkedInClient>();

// Repositories — register concrete type so HandleShare can inject it for rate limiting
builder.Services.AddSingleton<LinkedInRepository>();
builder.Services.AddSingleton<ILinkedInRepository>(sp => sp.GetRequiredService<LinkedInRepository>());
builder.Services.AddSingleton<IUserRepository, UserRepository>();

// JSON structured logging
builder.Logging.AddJsonConsole(options =>
{
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Snapp.Service.LinkedIn" }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .ExcludeFromDescription();

app.MapLinkedInEndpoints();

#if LAMBDA
await app.RunLambdaAsync();
#else
app.Run();
#endif
