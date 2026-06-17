using Azure.Diagnostics;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Indexer;

[DurableTask(nameof(IndexIdProvider))]
public class IndexIdProvider(
    IPodcastRepository podcastRepository,
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
                podcast => ((!podcast.Removed.IsDefined() || podcast.Removed == false) &&
                            podcast.IndexAllEpisodes) ||
                           podcast.EpisodeIncludeTitleRegex != "",
                podcast => new IndexablePodcastProjection(
                    podcast.Id,
                    podcast.ReleaseAuthority,
                    podcast.YouTubeChannelId,
                    podcast.SpotifyId,
                    podcast.AppleId))
            .ToArrayAsync();

        var allIndexablePodcastIds = indexablePodcasts.Select(p => p.Id).ToArray();
        var youtubeDiscoveryIds = indexablePodcasts
            .Where(p => PodcastExtensions.DependsOnYouTubeForEpisodeDiscovery(
                p.ReleaseAuthority, p.YouTubeChannelId, p.SpotifyId, p.AppleId))
            .Select(p => p.Id)
            .ToArray();

        if (youtubeDiscoveryIds.Length > 0)
        {
            logger.LogInformation(
                "YouTubeAuthorityIndexPool audit-utc='{AuditUtc:O}' podcast-count='{PodcastCount}' podcast-ids='{PodcastIds}'",
                DateTime.UtcNow, youtubeDiscoveryIds.Length, string.Join(",", youtubeDiscoveryIds));
        }

        var batchSizes = allIndexablePodcastIds.Length / req.IndexPasses;
        var batches = new List<Guid[]>();
        for (var i = 0; i < req.IndexPasses; i++)
        {
            var batch = allIndexablePodcastIds.Skip(i * batchSizes);
            if (i < req.IndexPasses - 1)
            {
                batch = batch.Take(batchSizes);
            }

            var batchArray = batch.ToArray();
            logger.LogInformation("Batch {i}: {batch}", i + 1, batchArray);
            batches.Add(batchArray);
        }

        var batchSum = batches.Sum(batch => batch.Length);
        if (batchSum != allIndexablePodcastIds.Length)
        {
            throw new InvalidOperationException(
                $"Batch sum {batchSum} does not equal all indexable podcast ids {allIndexablePodcastIds.Length}.");
        }

        logger.LogInformation($"{nameof(RunAsync)} Completed.");
        return new IndexIdProviderResponse(batches.ToArray());
    }

    private sealed record IndexablePodcastProjection(
        Guid Id,
        Service? ReleaseAuthority,
        string YouTubeChannelId,
        string SpotifyId,
        long? AppleId);
}
