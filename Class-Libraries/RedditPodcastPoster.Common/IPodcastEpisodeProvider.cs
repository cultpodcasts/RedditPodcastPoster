using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common;

public interface IPodcastEpisodeProvider
{
    Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        Guid podcastId);

    Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(
        Guid podcastId);
}