using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snapp.Shared.Configuration;

namespace Snapp.Shared.Hosting;

/// <summary>
/// Shared DI registration extensions used by all service Program.cs files.
/// These are contract stubs — implementations are provided by each service's
/// infrastructure layer (DynamoDB, KMS/local encryption, JWT validation).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DynamoDB client. Uses ServiceURL from configuration
    /// for local development (DynamoDB Local) or default AWS credentials for production.
    /// </summary>
    public static IServiceCollection AddSnappDynamo(this IServiceCollection services, IConfiguration configuration)
    {
        // Implementation provided by each service project.
        // Local dev: reads "DynamoDB:ServiceURL" (e.g., http://localhost:8042)
        // AWS: uses default credential chain + region
        return services;
    }

    /// <summary>
    /// Registers the <see cref="Snapp.Shared.Interfaces.IFieldEncryptor"/> implementation.
    /// Uses local file key for development or AWS KMS for production,
    /// determined by the "Encryption:Provider" configuration value.
    /// </summary>
    public static IServiceCollection AddSnappEncryption(this IServiceCollection services, IConfiguration configuration)
    {
        // Implementation provided by the encryption library (M1.1).
        // Local dev: LocalFileEncryptor reads key from "Encryption:KeyFilePath"
        // AWS: KmsEncryptor uses "Encryption:KmsKeyId"
        return services;
    }

    /// <summary>
    /// Registers JWT authentication and authorization services.
    /// Configures token validation parameters from configuration.
    /// </summary>
    public static IServiceCollection AddSnappAuth(this IServiceCollection services, IConfiguration configuration)
    {
        // Implementation provided by the auth library (M1.2).
        // Reads "Auth:Issuer", "Auth:Audience", "Auth:SigningKey"
        return services;
    }

    /// <summary>
    /// Loads a vertical configuration JSON file and registers it as a singleton VerticalConfig.
    /// The file is expected at the given path (e.g., Config/verticals/dental.json).
    /// Validates all DataAnnotation attributes on the deserialized model.
    /// </summary>
    public static IServiceCollection AddSnappVerticalConfig(this IServiceCollection services, string jsonFilePath)
    {
        var json = File.ReadAllText(jsonFilePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var config = JsonSerializer.Deserialize<VerticalConfig>(json, options)
            ?? throw new InvalidOperationException($"Failed to deserialize vertical config from {jsonFilePath}");

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(config);
        if (!Validator.TryValidateObject(config, context, validationResults, validateAllProperties: true))
        {
            var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new InvalidOperationException($"Vertical config validation failed: {errors}");
        }

        services.AddSingleton(config);
        return services;
    }

    /// <summary>
    /// Registers the <see cref="Snapp.Shared.Interfaces.IEmailSender"/> implementation.
    /// Uses SmtpEmailSender for development (Papercut) or SesEmailSender for production,
    /// determined by the "Email:Provider" configuration value.
    /// </summary>
    public static IServiceCollection AddSnappEmail(this IServiceCollection services, IConfiguration configuration)
    {
        // Implementation provided by the email library.
        // Local dev: SmtpEmailSender reads "Email:SmtpHost", "Email:SmtpPort"
        // AWS: SesEmailSender uses "Email:FromAddress", "Email:Region"
        return services;
    }
}
