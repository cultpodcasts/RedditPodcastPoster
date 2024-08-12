using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices;

public interface IPodcastEpisodeProvider
{
    Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed);
}