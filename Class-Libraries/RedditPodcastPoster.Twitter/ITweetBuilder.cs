using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Twitter;

public interface ITweetBuilder
{
    Task<string> BuildTweet(PodcastEpisode podcastEpisode, Uri? shortUrl);
}