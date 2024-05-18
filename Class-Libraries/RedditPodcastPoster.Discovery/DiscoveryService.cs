using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryService(
    ISearchProvider searchProvider,
    IEpisodeResultsAdapter episodeResultsAdapter,
    ILogger<DiscoveryService> logger
) : IDiscoveryService
{
    public async Task<IEnumerable<DiscoveryResult>> GetDiscoveryResults(
        IndexingContext indexingContext,
        DiscoveryConfig discoveryConfig)
    {
        logger.LogInformation($"{nameof(GetDiscoveryResults)} initiated.");
        var results = await searchProvider.GetEpisodes(indexingContext, discoveryConfig);
        return await episodeResultsAdapter.ToDiscoveryResults(results).ToListAsync();
    }
}