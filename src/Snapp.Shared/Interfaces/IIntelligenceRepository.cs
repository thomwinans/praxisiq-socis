using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

public interface IIntelligenceRepository
{
    Task SubmitDataAsync(PracticeData data);

    Task<List<PracticeData>> GetUserDataAsync(string userId);

    Task<Valuation?> GetCurrentValuationAsync(string userId);

    Task SaveValuationAsync(Valuation valuation);

    Task<List<Benchmark>> GetBenchmarksAsync(string specialty, string geography, string sizeBand);

    Task SaveBenchmarkAsync(Benchmark benchmark);
}
