using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public interface IPostManager
{
    Task RemoveEpisodePost(PodcastEpisode podcastEpisode);
    Task UpdateFlare(PodcastEpisode podcastEpisode);
}