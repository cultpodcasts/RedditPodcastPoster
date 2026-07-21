using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IEmbedCardRequestFactory
{
    Task<EmbedCardRequest?> CreateEmbedCardRequest(PodcastEpisode podcastEpisode, BlueskyEmbedCardPost embedPost);
}
