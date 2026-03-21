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
    public async IAsyncEnumerable<DiscoveryResult> GetDiscoveryResults(
        DiscoveryConfig discoveryConfig,
        IndexingContext indexingContext)
    {
        logger.LogInformation($"{nameof(GetDiscoveryResults)} initiated.");

        var results = await searchProvider.GetEpisodes(discoveryConfig, indexingContext);
        var enrichedResults = episodeResultsEnricher.EnrichWithPodcastDetails(results);

        await foreach (var item in enrichedEpisodeResultsAdapter.ToDiscoveryResults(enrichedResults))
        {
            yield return item;
        }
    }
}