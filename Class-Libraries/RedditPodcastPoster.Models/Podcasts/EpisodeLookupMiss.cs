using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// A remembered Index-flow catalogue miss for one episode on one platform. Fingerprint is the newest catalogue
/// episode id on page 0 plus the show total; while both are unchanged and
/// <see cref="LastFullAttemptUtc"/> is within the miss retry window, Index skips the full lookup.
/// Stored on <see cref="Podcast.LookupMisses"/> only — never on Episode, never on API DTOs.
/// </summary>
public sealed class EpisodeLookupMiss
{
    [JsonPropertyName("episodeId")]
    [JsonPropertyOrder(10)]
    public Guid EpisodeId { get; set; }

    /// <summary><c>Spotify</c> or <c>Apple</c> (<c>CataloguePlatforms</c>).</summary>
    [JsonPropertyName("platform")]
    [JsonPropertyOrder(20)]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("newestCatalogueId")]
    [JsonPropertyOrder(30)]
    public string? NewestCatalogueId { get; set; }

    [JsonPropertyName("showTotal")]
    [JsonPropertyOrder(40)]
    public int? ShowTotal { get; set; }

    [JsonPropertyName("lastFullAttemptUtc")]
    [JsonPropertyOrder(50)]
    public DateTime LastFullAttemptUtc { get; set; }
}
