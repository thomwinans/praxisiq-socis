using Snapp.Service.Enrichment.Models;

namespace Snapp.Service.Enrichment.Services;

public interface IJobPostingSource
{
    Task<List<JobPostingRecord>> GetJobPostingsAsync();
}
