using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Diagnostics;
using Indexer.Models;
using Indexer.Services;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Configuration.Options;

namespace Indexer.Activities;

public class Bluesky(
    IBlueskyPostManager blueskyPostManager,
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<IndexerOptions> indexerOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<Bluesky> logger)
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    [Function(nameof(Bluesky))]
    public async Task<IndexerContext> Run(
        [ActivityTrigger] IndexerContext indexerContext,
        FunctionContext functionContext)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(Bluesky));

        logger.LogInformation(
            "{BlueskyName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.", nameof(Bluesky),
            functionContext.InvocationId);
        logger.LogInformation(indexerContext.ToString());

        if (!activityOptionsProvider.RunBluesky(out var reason))
        {
            logger.LogWarning("{class} activity disabled. Reason: '{reason}'.", nameof(Bluesky), reason);
            return indexerContext with { Success = true };
        }
        else
        {
            logger.LogInformation("{class} activity enabled. Reason: '{reason}'.", nameof(Bluesky), reason);
        }

        try
        {
            await blueskyPostManager.Post(
                indexerContext is { SkipYouTubeUrlResolving: false, YouTubeError: false },
                indexerContext is { SkipSpotifyUrlResolving: false, SpotifyError: false },
                indexerContext.RecentEpisodeCandidates);
            logger.LogInformation("Bluesky-post executed");
        }
        catch (Exception ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            throw;
        }

        memoryProbe.End();

        logger.LogInformation("{method} Completed", nameof(Run));
        return indexerContext with { Success = true };
    }
}
