using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IEmbedCardRequestFactory
{
    Task<EmbedCardRequest?> CreateEmbedCardRequest(PodcastEpisodeV2 podcastEpisode, BlueskyEmbedCardPost embedPost);
}