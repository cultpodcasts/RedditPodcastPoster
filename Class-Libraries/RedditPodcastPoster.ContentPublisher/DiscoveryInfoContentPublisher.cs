using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.ContentPublisher;

public class DiscoveryInfoContentPublisher(
    IDiscoveryResultsRepository discoveryResultsRepository,
    IDiscoveryResultDeduplicator discoveryResultDeduplicator,
    IDiscoveryInfoRepository discoveryInfoRepository,
    IDiscoveryPublisher discoveryPublisher,
    ILogger<DiscoveryInfoContentPublisher> logger) : IDiscoveryInfoContentPublisher
{
    public async Task<DiscoveryInfo> PublishUnprocessedSummaryAsync(
        DateTime? lastSuccessfulDiscoveryBegan = null,
        CancellationToken cancellationToken = default)
    {
        var documents = await discoveryResultsRepository.GetAllUnprocessed().ToListAsync(cancellationToken);
        var dedupedResults = discoveryResultDeduplicator.Deduplicate(
            documents.SelectMany(x => x.DiscoveryResults));

        DateTime? discoveryBegan = null;
        int? numberOfResults = null;
        if (documents.Count > 0)
        {
            discoveryBegan = documents.Min(x => x.DiscoveryBegan);
            numberOfResults = dedupedResults.Count(x => !x.AutoHidden);
        }

        var preservedWatermark = lastSuccessfulDiscoveryBegan;
        if (preservedWatermark is null)
        {
            try
            {
                var existing = await discoveryInfoRepository.Get(cancellationToken);
                preservedWatermark = existing?.LastSuccessfulDiscoveryBegan;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "{Method}: failed to read existing discovery-info when preserving lastSuccessfulDiscoveryBegan.",
                    nameof(PublishUnprocessedSummaryAsync));
            }
        }

        var discoveryInfo = new DiscoveryInfo
        {
            DocumentCount = documents.Count,
            NumberOfResults = numberOfResults,
            DiscoveryBegan = discoveryBegan,
            LastSuccessfulDiscoveryBegan = preservedWatermark
        };

        await discoveryPublisher.PublishDiscoveryInfo(discoveryInfo);

        logger.LogInformation(
            "{Method} published discovery-info for {DocumentCount} document(s) and {VisibleResultCount} visible deduped result(s); lastSuccessfulDiscoveryBegan='{LastSuccessful}'.",
            nameof(PublishUnprocessedSummaryAsync),
            documents.Count,
            numberOfResults ?? 0,
            preservedWatermark?.ToString("O") ?? "none");

        return discoveryInfo;
    }
}
