using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Twitter.Dtos;

namespace RedditPodcastPoster.Twitter.Posters;

public interface ITweeter
{
    Task Tweet(
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        IReadOnlyList<PodcastEpisode>? preloadedRecentCandidates = null);
}
