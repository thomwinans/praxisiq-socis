using Snapp.Shared.DTOs.Transaction;

namespace Snapp.Service.Transaction.DTOs;

public class ReputationHistoryResponse
{
    public List<ReputationResponse> Snapshots { get; set; } = [];

    public string? NextToken { get; set; }
}
