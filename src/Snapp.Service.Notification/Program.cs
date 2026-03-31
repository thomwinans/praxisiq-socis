using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Snapp.Service.Notification.Endpoints;
using Snapp.Service.Notification.Repositories;
using Snapp.Service.Notification.Services;
using Snapp.Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

// JSON serialization — accept enum values as strings
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

// Repositories
builder.Services.AddSingleton<NotificationRepository>();
builder.Services.AddSingleton<INotificationRepository>(sp => sp.GetRequiredService<NotificationRepository>());

// JSON structured logging
builder.Logging.AddJsonConsole(options =>
{
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Snapp.Service.Notification" }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .ExcludeFromDescription();

app.MapNotificationEndpoints();

#if LAMBDA
await app.RunLambdaAsync();
#else
app.Run();
#endif
