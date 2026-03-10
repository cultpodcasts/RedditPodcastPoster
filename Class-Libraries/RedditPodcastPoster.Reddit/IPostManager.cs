using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public interface IPostManager
{
    Task RemoveEpisodePost(PodcastEpisodeV2 podcastEpisode);
    Task UpdateFlare(PodcastEpisodeV2 podcastEpisode);
}