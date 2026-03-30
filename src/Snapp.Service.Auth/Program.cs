using Amazon.DynamoDBv2;
using Snapp.Service.Auth.Endpoints;
using Snapp.Service.Auth.Repositories;
using Snapp.Service.Auth.Services;
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

// Email
builder.Services.AddSingleton<IEmailSender>(sp =>
    new SmtpEmailSender(sp.GetRequiredService<IConfiguration>()));

// JWT
builder.Services.AddSingleton<JwtTokenService>();

// Repositories
builder.Services.AddSingleton<IAuthRepository, AuthRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();

// JSON structured logging
builder.Logging.AddJsonConsole(options =>
{
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Snapp.Service.Auth" }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .ExcludeFromDescription();

app.MapAuthEndpoints();

#if LAMBDA
await app.RunLambdaAsync();
#else
app.Run();
#endif
