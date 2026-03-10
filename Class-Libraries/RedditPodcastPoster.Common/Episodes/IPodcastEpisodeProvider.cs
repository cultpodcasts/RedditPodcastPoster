using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// Provider for podcast episodes backed by detached `IEpisodeRepository` entities.
/// Returns `PodcastEpisode` values.
/// </summary>
public interface IPodcastEpisodeProvider
{
    Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(Guid podcastId);

    Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(Guid podcastId);
}

