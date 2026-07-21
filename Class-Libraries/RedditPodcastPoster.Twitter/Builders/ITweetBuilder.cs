using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Twitter.Builders;

public interface ITweetBuilder
{
    Task<string> BuildTweet(PodcastEpisode podcastEpisode, Uri? shortUrl);
}
