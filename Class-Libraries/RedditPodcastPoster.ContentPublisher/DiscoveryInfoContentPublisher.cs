using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.ContentPublisher;

public class DiscoveryInfoContentPublisher(
    IDiscoveryResultsRepository discoveryResultsRepository,
    IDiscoveryResultDeduplicator discoveryResultDeduplicator,
    IDiscoveryPublisher discoveryPublisher,
    ILogger<DiscoveryInfoContentPublisher> logger) : IDiscoveryInfoContentPublisher
{
    public async Task<DiscoveryInfo> PublishUnprocessedSummaryAsync(CancellationToken cancellationToken = default)
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

        var discoveryInfo = new DiscoveryInfo
        {
            DocumentCount = documents.Count,
            NumberOfResults = numberOfResults,
            DiscoveryBegan = discoveryBegan
        };

        await discoveryPublisher.PublishDiscoveryInfo(discoveryInfo);

        logger.LogInformation(
            "{Method} published discovery-info for {DocumentCount} document(s) and {VisibleResultCount} visible deduped result(s).",
            nameof(PublishUnprocessedSummaryAsync),
            documents.Count,
            numberOfResults ?? 0);

        return discoveryInfo;
    }
}
