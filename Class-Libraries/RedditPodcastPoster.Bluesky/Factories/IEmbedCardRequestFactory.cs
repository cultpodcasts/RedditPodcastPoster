using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IEmbedCardRequestFactory
{
    Task<EmbedCardRequest?> CreateEmbedCardRequest(PodcastEpisode podcastEpisode, BlueskyEmbedCardPost embedPost);
}