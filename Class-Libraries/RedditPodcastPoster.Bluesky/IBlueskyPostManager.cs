using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky;

public interface IBlueskyPostManager
{
    Task Post(
        bool youTubeRefreshed,
        bool spotifyRefreshed);

    Task<Models.RemovePostState> RemovePost(PodcastEpisodeV2 podcastEpisode);
}