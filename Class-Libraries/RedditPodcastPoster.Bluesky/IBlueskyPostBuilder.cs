using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky;

public interface IBlueskyPostBuilder
{
    Task<string> BuildPost(PodcastEpisode podcastEpisode, Uri? shortUrl);
}