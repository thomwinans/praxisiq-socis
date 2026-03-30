using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

/// <summary>
/// Data access contract for the snapp-intel DynamoDB table.
/// Handles practice data contributions, valuations, and benchmarks.
/// </summary>
public interface IIntelligenceRepository
{
    /// <summary>Stores a practice data contribution for a user, keyed by dimension and category.</summary>
    Task SubmitDataAsync(PracticeData data);

    /// <summary>Retrieves all practice data contributions for a user across all dimensions.</summary>
    Task<List<PracticeData>> GetUserDataAsync(string userId);

    /// <summary>Retrieves the current valuation for a user. Returns null if no valuation exists.</summary>
    Task<Valuation?> GetCurrentValuationAsync(string userId);

    /// <summary>Saves a valuation snapshot and updates the current valuation pointer.</summary>
    Task SaveValuationAsync(Valuation valuation);

    /// <summary>Retrieves benchmark metrics for a specialty/geography/size-band combination.</summary>
    Task<List<Benchmark>> GetBenchmarksAsync(string specialty, string geography, string sizeBand);

    /// <summary>Saves or updates a benchmark metric record.</summary>
    Task SaveBenchmarkAsync(Benchmark benchmark);
}
