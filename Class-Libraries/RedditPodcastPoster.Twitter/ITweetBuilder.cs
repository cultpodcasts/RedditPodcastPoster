using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Twitter;

public interface ITweetBuilder
{
    string BuildTweet(PodcastEpisode podcastEpisode);
}