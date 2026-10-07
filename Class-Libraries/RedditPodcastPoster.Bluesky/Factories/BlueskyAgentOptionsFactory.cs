using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.RichText;
using RedditPodcastPoster.Bluesky.RichText;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyAgentOptionsFactory : IBlueskyAgentOptionsFactory
{
    public BlueskyAgentOptions Create(Func<string, CancellationToken, Task<Did?>> resolveHandle)
    {
        ArgumentNullException.ThrowIfNull(resolveHandle);
        IFacetExtractor facetExtractor = new DashedHandleFacetExtractor(resolveHandle);
        return new BlueskyAgentOptions(loggerFactory: null, facetExtractor: facetExtractor);
    }
}
