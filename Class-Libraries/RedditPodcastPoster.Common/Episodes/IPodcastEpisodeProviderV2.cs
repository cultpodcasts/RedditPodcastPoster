using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// Provider for podcast episodes backed by detached `IEpisodeRepository` entities.
/// Returns `PodcastEpisodeV2` values.
/// </summary>
public interface IPodcastEpisodeProviderV2
{
    /// <summary>
    /// Gets all untweeted podcast episodes across podcasts released since the configured tweet window.
    /// </summary>
    Task<IEnumerable<PodcastEpisodeV2>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    /// <summary>
    /// Gets untweeted episodes for a specific podcast.
    /// </summary>
    Task<IEnumerable<PodcastEpisodeV2>> GetUntweetedPodcastEpisodes(Guid podcastId);

    /// <summary>
    /// Gets all Bluesky-ready podcast episodes across podcasts.
    /// </summary>
    Task<IEnumerable<PodcastEpisodeV2>> GetBlueskyReadyPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    /// <summary>
    /// Gets Bluesky-ready episodes for a specific podcast.
    /// </summary>
    Task<IEnumerable<PodcastEpisodeV2>> GetBlueskyReadyPodcastEpisodes(Guid podcastId);
}
