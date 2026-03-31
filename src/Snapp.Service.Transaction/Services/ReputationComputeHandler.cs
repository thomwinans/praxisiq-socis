using Snapp.Service.Transaction.Models;
using Snapp.Service.Transaction.Repositories;
using Snapp.Shared.Enums;
using Snapp.Shared.Models;

namespace Snapp.Service.Transaction.Services;

public class ReputationComputeHandler
{
    private readonly TransactionRepository _repo;
    private readonly ILogger<ReputationComputeHandler> _logger;

    private const decimal ReferralWeight = 0.40m;
    private const decimal ContributionWeight = 0.30m;
    private const decimal AttestationWeight = 0.30m;
    private const decimal MonthlyDecayRate = 0.05m;
    private const decimal MaxScore = 100m;

    public ReputationComputeHandler(TransactionRepository repo, ILogger<ReputationComputeHandler> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<Reputation> ComputeAsync(string userId)
    {
        var referralScore = await ComputeReferralScoreAsync(userId);
        var attestationResult = await ComputeAttestationScoreAsync(userId);
        var contributionScore = await GetContributionScoreAsync(userId);

        var overall = (referralScore * ReferralWeight)
                    + (contributionScore * ContributionWeight)
                    + (attestationResult.Score * AttestationWeight);

        // Apply decay for inactivity
        overall = ApplyDecay(overall, userId, await GetLastActivityDateAsync(userId));

        overall = Math.Clamp(overall, 0, MaxScore);

        var reputation = new Reputation
        {
            UserId = userId,
            OverallScore = Math.Round(overall, 2),
            ReferralScore = Math.Round(referralScore, 2),
            ContributionScore = Math.Round(contributionScore, 2),
            AttestationScore = Math.Round(attestationResult.Score, 2),
            ComputedAt = DateTime.UtcNow,
        };

        await _repo.SaveReputationAsync(reputation);

        _logger.LogInformation("Reputation computed for {UserId}: overall={Overall}, referral={Referral}, contribution={Contribution}, attestation={Attestation}",
            userId, reputation.OverallScore, reputation.ReferralScore, reputation.ContributionScore, reputation.AttestationScore);

        return reputation;
    }

    public async Task<AntiGamingResult> DetectAntiGamingAsync(string attestorUserId, string targetUserId)
    {
        // Check for reciprocal attestation ring
        var reverseAttestation = await _repo.GetAttestationAsync(attestorUserId, targetUserId);
        if (reverseAttestation != null)
        {
            _logger.LogWarning("Reciprocal attestation detected: {Attestor} <-> {Target}",
                attestorUserId, targetUserId);

            return new AntiGamingResult
            {
                Flagged = true,
                Reason = "Reciprocal attestation detected — both users have attested each other.",
            };
        }

        // Check if attestor has an unusually high number of attestations given
        var given = await _repo.ListAttestationsByUserAsync(attestorUserId);
        if (given.Count > 50)
        {
            _logger.LogWarning("High attestation volume from {Attestor}: {Count} attestations",
                attestorUserId, given.Count);

            return new AntiGamingResult
            {
                Flagged = true,
                Reason = $"Attestor has given {given.Count} attestations — possible gaming behavior.",
            };
        }

        return new AntiGamingResult { Flagged = false };
    }

    private async Task<decimal> ComputeReferralScoreAsync(string userId)
    {
        var sentReferrals = await _repo.ListSentReferralsAsync(userId, null);
        if (sentReferrals.Count == 0) return 0;

        var completed = sentReferrals.Where(r => r.Status == ReferralStatus.Completed).ToList();
        var total = sentReferrals.Count;

        if (total == 0) return 0;

        var successRate = (decimal)completed.Count / total;

        // Recency boost: referrals in last 90 days count more
        var recentCutoff = DateTime.UtcNow.AddDays(-90);
        var recentCompleted = completed.Count(r => r.CreatedAt >= recentCutoff);
        var recencyBonus = Math.Min(recentCompleted * 5m, 20m);

        // Base score: success rate * 80 (max 80) + recency bonus (max 20) = max 100
        var score = (successRate * 80m) + recencyBonus;
        return Math.Min(score, MaxScore);
    }

    private async Task<(decimal Score, bool HasFlags)> ComputeAttestationScoreAsync(string userId)
    {
        var attestations = await _repo.ListAttestationsForUserAsync(userId);
        if (attestations.Count == 0) return (0, false);

        decimal totalWeight = 0;
        foreach (var att in attestations)
        {
            // Weight by attestor's reputation (if available)
            var attestorRep = await _repo.GetReputationAsync(att.AttestorUserId);
            var weight = attestorRep != null ? (attestorRep.OverallScore / MaxScore) : 0.5m;
            totalWeight += weight;
        }

        // Score: weighted count, capped at 100
        // ~10 attestations from reputable users = full score
        var score = Math.Min((totalWeight / 10m) * MaxScore, MaxScore);
        return (score, false);
    }

    private async Task<decimal> GetContributionScoreAsync(string userId)
    {
        // Contribution score is computed from data outside the tx table
        // (content posts, data categories). For now, preserve any existing score.
        var existing = await _repo.GetReputationAsync(userId);
        return existing?.ContributionScore ?? 0;
    }

    private async Task<DateTime?> GetLastActivityDateAsync(string userId)
    {
        var sent = await _repo.ListSentReferralsAsync(userId, null);
        var attestations = await _repo.ListAttestationsForUserAsync(userId);

        var dates = new List<DateTime>();
        if (sent.Count > 0) dates.Add(sent.Max(r => r.CreatedAt));
        if (attestations.Count > 0) dates.Add(attestations.Max(a => a.CreatedAt));

        return dates.Count > 0 ? dates.Max() : null;
    }

    private static decimal ApplyDecay(decimal score, string userId, DateTime? lastActivity)
    {
        if (!lastActivity.HasValue) return score;

        var monthsInactive = (DateTime.UtcNow - lastActivity.Value).TotalDays / 30.0;
        if (monthsInactive <= 1) return score;

        var decayFactor = (decimal)Math.Pow((double)(1m - MonthlyDecayRate), monthsInactive - 1);
        return score * decayFactor;
    }
}

public class AntiGamingResult
{
    public bool Flagged { get; set; }
    public string? Reason { get; set; }
}
