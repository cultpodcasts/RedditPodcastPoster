using X.Bluesky;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IBlueskyClientFactory
{
    IBlueskyClient Create();
}