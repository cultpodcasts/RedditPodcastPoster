using idunno.Bluesky;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IBlueskyAgentFactory
{
    Task<BlueskyAgent> Create();
}
