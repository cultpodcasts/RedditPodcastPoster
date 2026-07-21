using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Posters;

public interface ITweetPoster
{
    Task<PostTweetResponse> PostTweet(PodcastEpisode podcastEpisode, Uri? shortUrl);
}
