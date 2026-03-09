using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Twitter;

public interface ITweetBuilder
{
    Task<string> BuildTweet(PodcastEpisodeV2 podcastEpisode, Uri? shortUrl);
}