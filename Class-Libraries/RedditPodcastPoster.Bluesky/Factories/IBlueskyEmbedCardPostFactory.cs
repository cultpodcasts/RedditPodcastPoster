using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IBlueskyEmbedCardPostFactory
{
    Task<BlueskyEmbedCardPost> Create(PodcastEpisodeV2 podcastEpisode, Uri? shortUrl);
}