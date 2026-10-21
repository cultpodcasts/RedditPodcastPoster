using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Bluesky.Managers;

public interface IBlueskyPostManager
{
    Task Post(
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        IReadOnlyList<PodcastEpisode>? preloadedRecentCandidates = null);

    Task<Models.RemovePostState> RemovePost(PodcastEpisode podcastEpisode);

    /// <summary>Same as the podcast overload: delete by stored AT URI, else by legacy text match.</summary>
    Task<Models.RemovePostState> RemovePost(SocialPostRemovalTarget target);
}
