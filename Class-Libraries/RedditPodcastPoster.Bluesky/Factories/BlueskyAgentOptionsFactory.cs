using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.RichText;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.RichText;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyAgentOptionsFactory(ILoggerFactory loggerFactory) : IBlueskyAgentOptionsFactory
{
    public BlueskyAgentOptions Create(Func<string, CancellationToken, Task<Did?>> resolveHandle)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(resolveHandle);
        IFacetExtractor facetExtractor = new DashedHandleFacetExtractor(resolveHandle);
        return new BlueskyAgentOptions(loggerFactory, facetExtractor: facetExtractor);
    }
}
