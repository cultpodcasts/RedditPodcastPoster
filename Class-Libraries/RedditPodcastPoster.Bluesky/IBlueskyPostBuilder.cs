using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky;

public interface IBlueskyPostBuilder
{
    Task<(string, Uri)> BuildPost(PodcastEpisode podcastEpisode, Uri? shortUrl);
}