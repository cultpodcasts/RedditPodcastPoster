using RedditPodcastPoster.Models;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter;

public interface ITweetPoster
{
    Task<PostTweetResponse> PostTweet(PodcastEpisode podcastEpisode, Uri? shortUrl);
}