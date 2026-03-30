using System.ComponentModel.DataAnnotations;

namespace Snapp.Shared.Models;

/// <summary>
/// Represents a channel relay configuration for a network.
/// PK: NET#{NetworkId}, SK: RELAY#{ChannelId}
/// Used to relay posts, milestones, and digests to Teams/Slack channels via email.
/// </summary>
public class ChannelRelay
{
    [Required]
    public string ChannelId { get; set; } = string.Empty;

    [Required]
    public string NetworkId { get; set; } = string.Empty;

    /// <summary>Encrypted channel email address (PII — encrypted via IFieldEncryptor).</summary>
    [Required]
    public string EncryptedChannelEmail { get; set; } = string.Empty;

    /// <summary>Platform: "Teams", "Slack", or "Other".</summary>
    [Required]
    public string Platform { get; set; } = string.Empty;

    /// <summary>Human-readable label for the channel.</summary>
    [MaxLength(100)]
    public string Label { get; set; } = string.Empty;

    /// <summary>Whether the channel email has been verified via confirmation email.</summary>
    public bool IsVerified { get; set; }

    /// <summary>Types of content relayed to this channel (e.g., "posts", "milestones", "digest").</summary>
    public List<string> RelayTypes { get; set; } = [];

    public DateTime CreatedAt { get; set; }
}
