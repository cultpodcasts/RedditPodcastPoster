using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common;

public interface IPodcastEpisodeProvider
{
    Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);
}