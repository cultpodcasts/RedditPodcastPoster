using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Twitter;

public interface ITweetBuilder
{
    Task<string> BuildTweet(PodcastEpisode podcastEpisode, Uri? shortUrl);
}
