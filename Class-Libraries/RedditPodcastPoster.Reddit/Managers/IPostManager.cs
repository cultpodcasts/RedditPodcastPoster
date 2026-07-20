using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit.Managers;

public interface IPostManager
{
    Task RemoveEpisodePost(PodcastEpisode podcastEpisode);
    Task UpdateFlare(PodcastEpisode podcastEpisode);
}
