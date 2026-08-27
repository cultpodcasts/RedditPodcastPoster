// pragma: allowlist secret
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret

/// <summary>
/// Spotify / Apple / YouTube episode identity. Presence of a service for matching
/// and search reconstruction lives here — not on a named URL slot.
/// </summary>
public class EpisodeIds // pragma: allowlist secret
{
    [JsonPropertyName("spotify")]
    public string? Spotify { get; set; }

    [JsonPropertyName("apple")]
    public long? Apple { get; set; }

    [JsonPropertyName("youtube")]
    public string? YouTube { get; set; }

    [JsonIgnore]
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Spotify) &&
        Apple is null &&
        string.IsNullOrWhiteSpace(YouTube);
}
