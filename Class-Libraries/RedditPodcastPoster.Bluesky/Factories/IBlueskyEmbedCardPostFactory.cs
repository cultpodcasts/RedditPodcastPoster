using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IBlueskyEmbedCardPostFactory
{
    Task<BlueskyEmbedCardPost> BuildPost(PodcastEpisode podcastEpisode, Uri? shortUrl);
}