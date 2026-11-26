using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Diagnostics;
using Indexer.Models;
using Indexer.Services;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration.Options;

namespace Indexer.Activities;

public class Poster(
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<PosterOptions> posterOptions,
    IOptions<PostingCriteria> postingCriteria,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<Poster> logger)
{
    private readonly PosterOptions _posterOptions = posterOptions.Value;
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    [Function(nameof(Poster))]
    public Task<IndexerContext> Run(
        [ActivityTrigger] IndexerContext indexerContext,
        FunctionContext functionContext)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(Poster));

        logger.LogInformation("{class} initiated. task-activity-context-instance-id: '{contextInstanceId}'.",
            nameof(Poster), functionContext.InvocationId);
        logger.LogInformation(indexerContext.ToString());
        logger.LogInformation(_posterOptions.ToString());
        logger.LogInformation(_postingCriteria.ToString());
        var baselineDate = DateTimeExtensions.DaysAgo(_posterOptions.ReleasedDaysAgo);

        logger.LogInformation(
            "{method} Posting with options released-since: '{baselineDate:O}', max-posts: '{posterOptionsMaxPosts}'.",
            nameof(Run), baselineDate, _posterOptions.MaxPosts);

        if (!activityOptionsProvider.RunPoster(out var reason))
        {
            logger.LogWarning("{class} activity disabled. Reason: '{reason}'.", nameof(Poster), reason);
            memoryProbe.End(true);
            return Task.FromResult(indexerContext with { Success = true });
        }

        logger.LogInformation("{class} activity enabled. Reason: '{reason}'.", nameof(Poster), reason);

        if (indexerContext.PosterOperationId == null)
        {
            memoryProbe.End(false, nameof(ArgumentNullException));
            throw new ArgumentNullException(nameof(indexerContext.PosterOperationId));
        }

        // Live Reddit.NET posting removed; RunPoster switch kept for a future Devvit poster.
        logger.LogInformation(
            "{method} Reddit posting is retired; skipping episode posts (released-since '{baselineDate:O}').",
            nameof(Run),
            baselineDate);

        var result = indexerContext with { Success = true };
        memoryProbe.End(true);
        logger.LogInformation("{method} Completed. Result: {result}", nameof(Run), result);
        return Task.FromResult(result);
    }
}
