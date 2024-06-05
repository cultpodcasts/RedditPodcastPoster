using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryService(
    ISearchProvider searchProvider,
    IEnrichedEpisodeResultsAdapter enrichedEpisodeResultsAdapter,
    IEpisodeResultsEnricher episodeResultsEnricher,
    ILogger<DiscoveryService> logger
) : IDiscoveryService
{
    public async Task<IEnumerable<DiscoveryResult>> GetDiscoveryResults(
        IndexingContext indexingContext,
        DiscoveryConfig discoveryConfig)
    {
        logger.LogInformation($"{nameof(GetDiscoveryResults)} initiated.");
        var results = await searchProvider.GetEpisodes(indexingContext, discoveryConfig);
        var enrichedResults = await episodeResultsEnricher.EnrichWithPodcastDetails(results).ToListAsync();
        return await enrichedEpisodeResultsAdapter.ToDiscoveryResults(enrichedResults).ToListAsync();
    }
}