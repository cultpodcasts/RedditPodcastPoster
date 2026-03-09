using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// V2 version of IPodcastEpisodeProvider that works with detached episodes via IEpisodeRepository.
/// </summary>
public interface IPodcastEpisodeProviderV2
{
    /// <summary>
    /// Gets all untweeted podcast episodes across podcasts released since the configured tweet window.
    /// </summary>
    Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    /// <summary>
    /// Gets untweeted episodes for a specific podcast.
    /// </summary>
    Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(Guid podcastId);

    /// <summary>
    /// Gets all Bluesky-ready podcast episodes across podcasts.
    /// </summary>
    Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    /// <summary>
    /// Gets Bluesky-ready episodes for a specific podcast.
    /// </summary>
    Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(Guid podcastId);
}
