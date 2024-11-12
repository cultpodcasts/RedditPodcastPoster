using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Twitter;

public interface ITweetPoster
{
    Task<TweetSendStatus> PostTweet(PodcastEpisode podcastEpisode, Uri? shortUrl);
}