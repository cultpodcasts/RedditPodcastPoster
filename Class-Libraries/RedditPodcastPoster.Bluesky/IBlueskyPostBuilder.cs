using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky;

public interface IBlueskyPostBuilder
{
    Task<BlueskyEmbedCardPost> BuildPost(PodcastEpisode podcastEpisode, Uri? shortUrl);
}