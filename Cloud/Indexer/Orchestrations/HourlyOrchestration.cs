using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Indexer.Activities;
using Indexer.Models;

namespace Indexer.Orchestrations;

[DurableTask(nameof(HourlyOrchestration))]
public class HourlyOrchestration : TaskOrchestrator<HourlyOrchestrationRunInput?, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(
        TaskOrchestrationContext context,
        HourlyOrchestrationRunInput? input)
    {
        var logger = context.CreateReplaySafeLogger<HourlyOrchestration>();
        logger.LogInformation(
            "{nameofHourlyOrchestration}.{nameofRunAsync} initiated. Instance-id: '{contextInstanceId}'.",
            nameof(HourlyOrchestration), nameof(RunAsync), context.InstanceId);

        // Stale-run guard: an instance scheduled for an earlier UTC hour (e.g. a Pending backlog
        // draining after a broken host was fixed) must no-op instead of re-running the pipeline
        // and duplicating social posts. Deterministic: input is fixed and CurrentUtcDateTime is
        // replay-safe. Instances scheduled by older code carry no input and run unguarded.
        if (input is not null &&
            HourlyOrchestrationCatchUpEvaluator.IsStaleRun(input.ScheduledAtUtc, context.CurrentUtcDateTime))
        {
            logger.LogWarning(
                "HourlyOrchestration stale-run-skipped instance-id='{InstanceId}' scheduled-at-utc='{ScheduledAtUtc:O}' current-utc='{CurrentUtc:O}'.",
                context.InstanceId, input.ScheduledAtUtc, context.CurrentUtcDateTime);
            return new IndexerContext(Success: true);
        }

        const int indexPasses = 4;
        var indexPassOperationIs = Enumerable.Range(1, indexPasses).Select(_ => context.NewGuid()).ToArray();

        var indexIds = await context.CallIndexIdProviderAsync(new IndexIdProviderRequest(indexPasses));
        logger.LogInformation("{nameofIndexIdProvider} complete.", nameof(IndexIdProvider));

        var hourUtc = context.CurrentUtcDateTime.Hour;
        var (firstPass, lastPass) = HourlyIndexingPassSelector.SelectPasses(hourUtc, indexPasses);
        var youtubeEnabledHour = hourUtc % 3 == 0;
        logger.LogWarning(
            "HourlyOrchestration pass-selection hour-utc='{HourUtc}' first-pass='{FirstPass}' last-pass='{LastPass}' youtube-enabled-hour='{YouTubeEnabledHour}' orchestration-instance-id='{OrchestrationInstanceId}'",
            hourUtc, firstPass, lastPass, youtubeEnabledHour, context.InstanceId);
        logger.LogWarning(
            "HourlyOrchestration indexer-operation-ids pass-1='{Pass1OperationId}' pass-2='{Pass2OperationId}' pass-3='{Pass3OperationId}' pass-4='{Pass4OperationId}'",
            indexPassOperationIs[0], indexPassOperationIs[1], indexPassOperationIs[2], indexPassOperationIs[3]);

        var indexerContext = new IndexerContext(indexPassOperationIs, indexIds.PodcastIdBatches)
        {
            CatalogueParentIndexIds = indexIds.CatalogueParentIdBatches
        };
        var failedPasses = new List<int>();
        for (var pass = firstPass; pass <= lastPass; pass++)
        {
            try
            {
                indexerContext = await context.CallIndexerAsync(new IndexerContextWrapper(indexerContext, pass));
            }
            catch (TaskFailedException ex)
            {
                // An Indexer activity that fails (e.g. killed at the function timeout) must not cost the hour its
                // publish: log it, hold back posts that need fresh index data (as HalfHourlyOrchestration does) and
                // carry on. The orchestration still ends Failed below so the health check reports the hour.
                failedPasses.Add(pass);
                logger.LogError(
                    IndexerPassFailedMessagePrefix +
                    " pass='{Pass}' hour-utc='{HourUtc}' operation-id='{OperationId}' error-type='{ErrorType}' error='{ErrorMessage}'. Continuing to publish.",
                    pass, hourUtc, indexerContext.IndexerPassOperationIds?[pass - 1],
                    ex.FailureDetails?.ErrorType, ex.FailureDetails?.ErrorMessage);
                indexerContext = indexerContext with { Success = false };
                continue;
            }

            var batchPodcastCount = indexerContext.IndexIds?[pass - 1]?.Length ?? 0;
            var operationId = indexerContext.IndexerPassOperationIds?[pass - 1];
            logger.LogWarning(
                "HourlyOrchestration indexer-pass-complete pass='{Pass}' hour-utc='{HourUtc}' operation-id='{OperationId}' podcast-count='{PodcastCount}' success='{Success}' skip-youtube='{SkipYouTube}' youtube-error='{YouTubeError}'",
                pass, hourUtc, operationId, batchPodcastCount, indexerContext.Success,
                indexerContext.SkipYouTubeUrlResolving, indexerContext.YouTubeError);

            if (pass == 4)
            {
                logger.LogWarning(
                    "HourlyOrchestration batch-4-rollup hour-utc='{HourUtc}' operation-id='{OperationId}' podcast-count='{PodcastCount}' success='{Success}' skip-youtube='{SkipYouTube}' youtube-error='{YouTubeError}'",
                    hourUtc, operationId, batchPodcastCount, indexerContext.Success,
                    indexerContext.SkipYouTubeUrlResolving, indexerContext.YouTubeError);
            }

            logger.LogInformation("{nameofIndexer} complete - Pass {pass}.", nameof(Indexer), pass);
        }

        if (failedPasses.Count > 0)
        {
            indexerContext = WithPostingHeldBack(indexerContext);
        }

        indexerContext = await context.CallLoadRecentCandidatesAsync(indexerContext);
        logger.LogInformation("{nameofLoadRecentCandidates} complete.", nameof(LoadRecentCandidates));

        indexerContext =
            await context.CallCategoriserAsync(indexerContext with {CategoriserOperationId = context.NewGuid()});
        logger.LogInformation("{nameofCategoriser} complete.", nameof(Categoriser));

        indexerContext = await context.CallYouTubeThumbnailRecheckAsync(indexerContext);
        logger.LogInformation("{nameofYouTubeThumbnailRecheck} complete.", nameof(YouTubeThumbnailRecheck));

        indexerContext = await context.CallPosterAsync(indexerContext with {PosterOperationId = context.NewGuid()});
        logger.LogInformation("{nameofPoster} complete.", nameof(Poster));

        indexerContext =
            await context.CallPublisherAsync(indexerContext with {PublisherOperationId = context.NewGuid()});
        logger.LogInformation("{nameofPublisher} complete.", nameof(Publisher));

        indexerContext = await context.CallTweetAsync(indexerContext with {TweetOperationId = context.NewGuid()});
        logger.LogInformation("{nameofTweet} complete.", nameof(Tweet));

        indexerContext = await context.CallBlueskyAsync(indexerContext with {BlueskyOperationId = context.NewGuid()});
        logger.LogInformation("{nameofBluesky} complete. All tasks complete.", nameof(Bluesky));

        if (failedPasses.Count > 0)
        {
            throw new IndexerPassesFailedException(failedPasses);
        }

        return indexerContext;
    }

    /// <summary>Stable log prefix for an Indexer activity failure the orchestration continued past.</summary>
    public const string IndexerPassFailedMessagePrefix = "HourlyOrchestration indexer-pass-failed";

    /// <summary>
    /// After a failed pass the hour's YouTube/Spotify state is unknown: flag both as errored so Tweet, Bluesky and the
    /// thumbnail re-check only act on items that do not need this hour's index (the HalfHourlyOrchestration hold-back).
    /// </summary>
    public static IndexerContext WithPostingHeldBack(IndexerContext indexerContext) =>
        indexerContext with
        {
            Success = false,
            YouTubeError = true,
            SpotifyError = true
        };
}

/// <summary>
/// Thrown at the end of an hourly run whose Indexer pass(es) failed after the rest of the pipeline ran, so the
/// instance is recorded as Failed (and reported by <see cref="HourlyOrchestrationHealthChecker"/>).
/// </summary>
public class IndexerPassesFailedException(IReadOnlyList<int> failedPasses)
    : Exception($"Indexer pass(es) {string.Join(", ", failedPasses)} failed; the rest of the hourly pipeline ran.")
{
    public IReadOnlyList<int> FailedPasses { get; } = failedPasses;
}