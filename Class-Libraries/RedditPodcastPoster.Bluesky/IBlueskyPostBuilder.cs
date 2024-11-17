using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky;

public interface IBlueskyPostBuilder
{
    Task<(string, Uri, Service)> BuildPost(PodcastEpisode podcastEpisode, Uri? shortUrl);
}