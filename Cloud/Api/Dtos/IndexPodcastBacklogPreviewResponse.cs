using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Query read model for <c>GET podcast/index/{name}/backlog/preview</c>: what a backlog Index would add, counted without
/// saving anything (cultpodcasts/RedditPodcastPoster#1433).
/// </summary>
public class IndexPodcastBacklogPreviewResponse
{
    /// <summary>
    /// Episodes the backlog Index would add: found on YouTube, Spotify or Apple, not already stored, not owned by a
    /// film / TV / news item, not dropped by the short-episode floor, not removed by elimination terms.
    /// </summary>
    [JsonPropertyName("newEpisodeCount")]
    public required int NewEpisodeCount { get; set; }

    /// <summary>The subset of <see cref="NewEpisodeCount"/> that would be saved as ignored (e.g. ignore-all-episodes).</summary>
    [JsonPropertyName("ignoredEpisodeCount")]
    public required int IgnoredEpisodeCount { get; set; }
}
