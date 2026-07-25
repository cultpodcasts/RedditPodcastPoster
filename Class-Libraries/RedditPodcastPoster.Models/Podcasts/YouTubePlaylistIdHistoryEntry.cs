using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// A former <see cref="Podcast.YouTubePlaylistId"/> retained when the configured playlist is swapped
/// so operators can recover if the new playlist was wrong.
/// </summary>
public class YouTubePlaylistIdHistoryEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>UTC instant when this id stopped being the active <see cref="Podcast.YouTubePlaylistId"/>.</summary>
    [JsonPropertyName("replacedAt")]
    public DateTime ReplacedAt { get; set; }
}
