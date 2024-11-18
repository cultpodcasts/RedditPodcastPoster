using RedditPodcastPoster.Bluesky.Client;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IBlueskyClientFactory
{
    IEmbedCardBlueskyClient Create();
}