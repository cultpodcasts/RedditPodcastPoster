using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryService(
    ISearchProvider searchProvider,
    IEpisodeResultsAdapter episodeResultsAdapter,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<DiscoveryService> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IDiscoveryService
{
    public async Task<IEnumerable<DiscoveryResult>> GetDiscoveryResults(IndexingContext indexingContext,
        DiscoveryConfig discoveryConfig)
    {
        var results = await searchProvider.GetEpisodes(indexingContext, discoveryConfig);
        return await episodeResultsAdapter.ToDiscoveryResults(results).ToListAsync();
    }
}