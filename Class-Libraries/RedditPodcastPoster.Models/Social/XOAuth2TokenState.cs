using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Social;

/// <summary>
/// Cosmos LookUps singleton holding the X (Twitter) OAuth 2.0 user-context token pair for the posting account, plus
/// the single-refresher lease. X refresh tokens are single-use and rotate, so the newest pair must always be saved
/// here before the new access token is used. Every write is etag-checked so only one host (Indexer, API or console)
/// refreshes at a time. Tokens are secrets: never log them, and never add this type to a download or export.
/// </summary>
[CosmosSelector(ModelType.XOAuth2TokenState)]
public sealed class XOAuth2TokenState : CosmosSelector, IFileKeyed
{
    public static readonly Guid _Id = Guid.Parse("5b0f7c1e-2d4a-4f6b-9e83-1c7a2d9f4e60");

    public XOAuth2TokenState()
    {
        Id = _Id;
        ModelType = ModelType.XOAuth2TokenState;
    }

    [JsonPropertyName("accessToken")]
    [JsonPropertyOrder(10)]
    public string? AccessToken { get; set; }

    [JsonPropertyName("accessTokenExpiresUtc")]
    [JsonPropertyOrder(11)]
    public DateTime? AccessTokenExpiresUtc { get; set; }

    [JsonPropertyName("refreshToken")]
    [JsonPropertyOrder(12)]
    public string? RefreshToken { get; set; }

    /// <summary>Space-separated scopes X granted with the current pair.</summary>
    [JsonPropertyName("scope")]
    [JsonPropertyOrder(13)]
    public string? Scope { get; set; }

    [JsonPropertyName("updatedUtc")]
    [JsonPropertyOrder(14)]
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>How the current pair was obtained: <c>exchange</c> (OAuth 1.0a token exchange) or <c>refresh</c>.</summary>
    [JsonPropertyName("source")]
    [JsonPropertyOrder(15)]
    public string? Source { get; set; }

    /// <summary>The refresh attempt that holds the lease, or null when no refresh is running.</summary>
    [JsonPropertyName("refreshLeaseAttemptId")]
    [JsonPropertyOrder(16)]
    public Guid? RefreshLeaseAttemptId { get; set; }

    /// <summary>When the lease lapses; a lease past this time belongs to a refresher that died and may be taken over.</summary>
    [JsonPropertyName("refreshLeaseUntilUtc")]
    [JsonPropertyOrder(17)]
    public DateTime? RefreshLeaseUntilUtc { get; set; }

    [JsonIgnore]
    public bool HasTokens => !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(RefreshToken);

    public bool IsRefreshLeaseHeld(DateTime nowUtc) =>
        RefreshLeaseAttemptId.HasValue && RefreshLeaseUntilUtc.HasValue && RefreshLeaseUntilUtc.Value > nowUtc;

    public bool IsAccessTokenValid(DateTime nowUtc, TimeSpan skew) =>
        !string.IsNullOrWhiteSpace(AccessToken) && AccessTokenExpiresUtc.HasValue &&
        AccessTokenExpiresUtc.Value - skew > nowUtc;

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(XOAuth2TokenState);
}
