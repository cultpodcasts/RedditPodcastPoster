using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IBlueskyEmbedCardPostFactory
{
    Task<BlueskyEmbedCardPost> Create(PodcastEpisode podcastEpisode, Uri? shortUrl);
}
