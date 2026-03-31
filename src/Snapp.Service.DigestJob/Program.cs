using Amazon.DynamoDBv2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snapp.Service.DigestJob;
using Snapp.Service.DigestJob.Services;
using Snapp.Shared.Interfaces;

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

// Encryption
services.AddSingleton<IFieldEncryptor>(sp =>
    new LocalFileEncryptor(config));

// Email
services.AddSingleton<IEmailSender>(sp =>
    new SmtpEmailSender(config));

// Processor
services.AddSingleton<DigestProcessor>();

var sp = services.BuildServiceProvider();
var processor = sp.GetRequiredService<DigestProcessor>();
var logger = sp.GetRequiredService<ILogger<Program>>();

// --now flag: run immediately for the current hour (dev/testing)
// --hour HH: run for a specific hour
var now = args.Contains("--now");
var hourIndex = Array.IndexOf(args, "--hour");
string? overrideHour = null;

if (hourIndex >= 0 && hourIndex + 1 < args.Length)
    overrideHour = args[hourIndex + 1];
else if (now)
    overrideHour = DateTime.UtcNow.Hour.ToString("D2");

logger.LogInformation("Digest job starting. Override hour: {Hour}", overrideHour ?? "(current)");

await processor.RunAsync(overrideHour);

logger.LogInformation("Digest job finished");
