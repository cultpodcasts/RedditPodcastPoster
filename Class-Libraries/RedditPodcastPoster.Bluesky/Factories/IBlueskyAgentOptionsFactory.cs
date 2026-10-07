using idunno.AtProto;
using idunno.Bluesky;

namespace RedditPodcastPoster.Bluesky.Factories;

public interface IBlueskyAgentOptionsFactory
{
    BlueskyAgentOptions Create(Func<string, CancellationToken, Task<Did?>> resolveHandle);
}
