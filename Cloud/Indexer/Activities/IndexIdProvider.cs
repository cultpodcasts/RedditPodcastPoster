using Microsoft.Azure.Cosmos.Linq;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Diagnostics;
using Indexer.Models;
using Indexer.Services;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;

namespace Indexer.Activities;

[DurableTask(nameof(IndexIdProvider))]
public class IndexIdProvider(
    IPodcastRepository podcastRepository,
    ICatalogueParentsUpdater catalogueParentsUpdater,
    IOptions<IndexerOptions> indexerOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<IndexIdProvider> logger
) : TaskActivity<IndexIdProviderRequest, IndexIdProviderResponse>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    public override async Task<IndexIdProviderResponse> RunAsync(TaskActivityContext context, IndexIdProviderRequest req)
    {
        logger.LogInformation(
            "{IndexIdProviderName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.", nameof(IndexIdProvider), context.InstanceId);

        if (req.IndexPasses < 1)
        {
            throw new ArgumentException("IndexPasses must be greater than 0.");
        }

        var indexablePodcasts = await podcastRepository
            .GetAllBy(
                podcast => (!podcast.Removed.IsDefined() || podcast.Removed == null || podcast.Removed == false) &&
                           (podcast.IndexAllEpisodes ||
                            podcast.EpisodeIncludeTitleRegex != ""))
            .ToArrayAsync();

        var allIndexablePodcastIds = indexablePodcasts.Select(p => p.Id).ToArray();
        var youtubeDiscoveryIds = indexablePodcasts
            .Where(p => p.DependsOnYouTubeForEpisodeDiscovery())
            .Select(p => p.Id)
            .ToArray();

        if (youtubeDiscoveryIds.Length > 0)
        {
            logger.LogInformation(
                "YouTubeAuthorityIndexPool audit-utc='{AuditUtc:O}' podcast-count='{PodcastCount}' podcast-ids='{PodcastIds}'",
                DateTime.UtcNow, youtubeDiscoveryIds.Length, string.Join(",", youtubeDiscoveryIds));
        }

        var batches = IndexPassBatchSplitter.Split(allIndexablePodcastIds, req.IndexPasses);
        for (var i = 0; i < batches.Length; i++)
        {
            var batchArray = batches[i];
            logger.LogInformation("Batch {i}: {batch}", i + 1, batchArray);
            if (i == 3)
            {
                logger.LogWarning(
                    "IndexIdProvider batch-4-summary podcast-count='{PodcastCount}'",
                    batchArray.Length);
            }
        }

        var catalogueParentIds = await catalogueParentsUpdater.GetIndexableParentIds();
        var catalogueParentBatches = IndexPassBatchSplitter.Split(catalogueParentIds, req.IndexPasses);
        logger.LogInformation(
            "IndexIdProvider catalogue-parent-count='{CatalogueParentCount}'", catalogueParentIds.Length);

        logger.LogInformation($"{nameof(RunAsync)} Completed.");
        return new IndexIdProviderResponse(batches, catalogueParentBatches);
    }
}
