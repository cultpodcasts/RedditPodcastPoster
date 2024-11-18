using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IEmbedCardFactory
{
    Task<EmbedCardRequest?> EmbedCardRequest(PodcastEpisode podcastEpisode, BlueskyEmbedCardPost embedPost);
}