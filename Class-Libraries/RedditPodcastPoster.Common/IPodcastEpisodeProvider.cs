using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common;

public interface IPodcastEpisodeProvider
{
    Task<IEnumerable<PodcastEpisodeV2>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    Task<IEnumerable<PodcastEpisodeV2>> GetUntweetedPodcastEpisodes(
        Guid podcastId);

    Task<IEnumerable<PodcastEpisodeV2>> GetBlueskyReadyPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    Task<IEnumerable<PodcastEpisodeV2>> GetBlueskyReadyPodcastEpisodes(
        Guid podcastId);
}