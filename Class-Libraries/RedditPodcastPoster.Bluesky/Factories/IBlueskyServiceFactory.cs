using idunno.Bluesky;
using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IBlueskyAgentFactory : IAsyncFactory<BlueskyAgent>
{
}
