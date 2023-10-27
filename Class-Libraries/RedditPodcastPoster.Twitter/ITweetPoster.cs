using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Twitter;

public interface ITweetPoster
{
    Task PostTweet(PodcastEpisode podcastEpisode);
}