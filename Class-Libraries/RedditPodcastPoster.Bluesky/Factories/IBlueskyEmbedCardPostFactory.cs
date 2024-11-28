using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IBlueskyEmbedCardPostFactory
{
    Task<BlueskyEmbedCardPost> Create(PodcastEpisode podcastEpisode, Uri? shortUrl);
}