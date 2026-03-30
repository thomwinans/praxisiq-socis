using System.Diagnostics;
using Xunit;

namespace Snapp.TestHelpers;

/// <summary>
/// Shared xUnit fixture that verifies the Docker Compose stack is running
/// and provides base URLs for all infrastructure services.
/// </summary>
public sealed class DockerTestFixture : IAsyncLifetime
{
    private static readonly string ComposeFilePath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Snapp.Infrastructure", "Docker", "docker-compose.yml"));

    public string DynamoDbUrl => "http://localhost:8042";
    public string KongUrl => "http://localhost:8000";
    public string MinioUrl => "http://localhost:9000";
    public string PapercutUrl => "http://localhost:8025";
    public string PapercutSmtpHost => "localhost";
    public int PapercutSmtpPort => 1025;

    public PapercutClient PapercutClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var isRunning = await CheckStackHealthAsync();

        if (!isRunning)
        {
            await StartDockerComposeAsync();
            await WaitForHealthAsync(timeoutSeconds: 60);
        }

        PapercutClient = new PapercutClient(PapercutUrl);

        // Clear inbox for test isolation
        try
        {
            await PapercutClient.DeleteAllMessagesAsync();
        }
        catch
        {
            // Papercut may not have any messages; ignore errors on delete
        }
    }

    public Task DisposeAsync()
    {
        PapercutClient?.Dispose();
        return Task.CompletedTask;
    }

    private async Task<bool> CheckStackHealthAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            // Check DynamoDB Local
            var dynamoResponse = await http.GetAsync(DynamoDbUrl);

            // Check Papercut web UI
            var papercutResponse = await http.GetAsync(PapercutUrl);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task StartDockerComposeAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"compose -f \"{ComposeFilePath}\" up -d",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start docker compose");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException(
                $"docker compose up failed (exit {process.ExitCode}): {stderr}");
        }
    }

    private async Task WaitForHealthAsync(int timeoutSeconds)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            if (await CheckStackHealthAsync())
                return;

            await Task.Delay(2000);
        }

        throw new TimeoutException(
            $"Docker Compose stack did not become healthy within {timeoutSeconds}s");
    }
}

[CollectionDefinition(Name)]
public class DockerTestCollection : ICollectionFixture<DockerTestFixture>
{
    public const string Name = "Docker";
}
