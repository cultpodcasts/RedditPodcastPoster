using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Bluesky;

public interface IBlueskyPostManager
{
    Task Post(
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        IReadOnlyList<PodcastEpisode>? preloadedRecentCandidates = null);

    Task<Models.RemovePostState> RemovePost(PodcastEpisode podcastEpisode);
}
