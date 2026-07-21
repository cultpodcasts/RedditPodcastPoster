using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Bluesky.Managers;

public interface IBlueskyPostManager
{
    Task Post(
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        IReadOnlyList<PodcastEpisode>? preloadedRecentCandidates = null);

    Task<Models.RemovePostState> RemovePost(PodcastEpisode podcastEpisode);
}
