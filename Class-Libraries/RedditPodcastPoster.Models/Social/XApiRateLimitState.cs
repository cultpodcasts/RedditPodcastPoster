using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Social;

/// <summary>
/// Cosmos LookUps singleton that remembers when X (Twitter) must not be called again after a rate-limit or
/// usage-cap response. Durable Functions recycle workers between hourly runs, so an in-memory flag alone would
/// keep burning credits; this document is the source of truth across Indexer instances. Not
/// <see cref="IFileKeyed"/>: it is operational state, not a backup artefact (same stance as
/// <see cref="XOAuth2TokenState"/>).
/// </summary>
[CosmosSelector(ModelType.XApiRateLimitState)]
public sealed class XApiRateLimitState : CosmosSelector
{
    public static readonly Guid _Id = Guid.Parse("a4e8c2b1-6f39-4d7a-9c15-8e2b0d7f1a63");

    public XApiRateLimitState()
    {
        Id = _Id;
        ModelType = ModelType.XApiRateLimitState;
    }

    /// <summary>
    /// Do not call X until this instant (UTC). Taken from the <c>x-rate-limit-reset</c> Unix-seconds header when
    /// present. Unspecified kind means UTC.
    /// </summary>
    [JsonPropertyName("rateLimitResetUtc")]
    [JsonPropertyOrder(10)]
    public DateTime? RateLimitResetUtc { get; set; }

    /// <summary>
    /// Do not call X until this instant (UTC) after a usage-capped / out-of-credits response. Distinct from
    /// <see cref="RateLimitResetUtc"/> because usage caps rarely carry a reset header. Unspecified kind means UTC.
    /// </summary>
    [JsonPropertyName("usageCappedUntilUtc")]
    [JsonPropertyOrder(11)]
    public DateTime? UsageCappedUntilUtc { get; set; }

    [JsonPropertyName("updatedUtc")]
    [JsonPropertyOrder(12)]
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>Last recorded block reason for logs (<c>rate-limit</c> or <c>usage-capped</c>).</summary>
    [JsonPropertyName("lastReason")]
    [JsonPropertyOrder(13)]
    public string? LastReason { get; set; }
}
