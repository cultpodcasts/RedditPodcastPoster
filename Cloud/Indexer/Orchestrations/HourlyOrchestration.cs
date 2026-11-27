using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Indexer.Activities;
using Indexer.Models;

namespace Indexer.Orchestrations;

public class HourlyOrchestration
{
    [Function(nameof(HourlyOrchestration))]
    public static async Task<IndexerContext> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        HourlyOrchestrationRunInput? input)
    {
        var logger = context.CreateReplaySafeLogger<HourlyOrchestration>();
        logger.LogInformation(
            "{nameofHourlyOrchestration}.{nameofRun} initiated. Instance-id: '{contextInstanceId}'.",
            nameof(HourlyOrchestration), nameof(Run), context.InstanceId);

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

        var indexIds = await context.CallActivityAsync<IndexIdProviderResponse>(
            nameof(IndexIdProvider), new IndexIdProviderRequest(indexPasses));
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

        // Parallel mode comes from IndexIdProvider (an activity), never from config — orchestrator determinism.
        var mode = indexIds.ParallelPasses;
        var (parallel, parallelReason) = HourlyParallelPasses.Decide(mode, hourUtc);
        logger.LogWarning(
            "HourlyOrchestration parallel-passes='{Parallel}' mode='{Mode}' reason='{Reason}' hour-utc='{HourUtc}' orchestration-instance-id='{InstanceId}'",
            parallel, mode, parallelReason, hourUtc, context.InstanceId);

        var indexerContext = new IndexerContext(indexPassOperationIs, indexIds.PodcastIdBatches)
        {
            CatalogueParentIndexIds = indexIds.CatalogueParentIdBatches
        };
        var failedPasses = new List<int>();

        if (!parallel)
        {
            for (var pass = firstPass; pass <= lastPass; pass++)
            {
                try
                {
                    indexerContext = await context.CallActivityAsync<IndexerContext>(
                        nameof(global::Indexer.Activities.Indexer), new IndexerContextWrapper(indexerContext, pass));
                }
                catch (TaskFailedException ex)
                {
                    // An Indexer activity that fails (e.g. killed at the function timeout) must not cost the hour its
                    // homepage publish: log it and carry on with the remaining passes, Categoriser, the thumbnail
                    // re-check (with YouTube flagged errored, so it re-resolves nothing) and Publisher. Poster, Tweet and
                    // Bluesky are skipped. The orchestration still ends Failed below so the health check reports the hour.
                    failedPasses.Add(pass);
                    LogIndexerPassFailed(logger, pass, hourUtc, indexerContext, ex);
                    indexerContext = indexerContext with { Success = false };
                    continue;
                }

                LogIndexerPassComplete(logger, pass, hourUtc, indexerContext);
            }
        }
        else
        {
            var seed = indexerContext;
            var tasks = Enumerable.Range(firstPass, lastPass - firstPass + 1)
                .Select(pass => (
                    Pass: pass,
                    Task: context.CallActivityAsync<IndexerContext>(
                        nameof(global::Indexer.Activities.Indexer),
                        new IndexerContextWrapper(seed, pass))))
                .ToList();

            var results = new List<IndexerContext>();
            foreach (var (pass, task) in tasks)
            {
                try
                {
                    var passResult = await task;
                    results.Add(passResult);
                    LogIndexerPassComplete(logger, pass, hourUtc, passResult);
                }
                catch (TaskFailedException ex)
                {
                    failedPasses.Add(pass);
                    LogIndexerPassFailed(logger, pass, hourUtc, seed, ex);
                }
            }

            indexerContext = IndexerContextMerge.Merge(seed, results);
            if (failedPasses.Count > 0)
            {
                indexerContext = indexerContext with { Success = false };
            }
        }

        if (failedPasses.Count > 0)
        {
            indexerContext = WithPostingHeldBack(indexerContext);
        }

        indexerContext = await context.CallActivityAsync<IndexerContext>(nameof(LoadRecentCandidates), indexerContext);
        logger.LogInformation("{nameofLoadRecentCandidates} complete.", nameof(LoadRecentCandidates));

        indexerContext = await context.CallActivityAsync<IndexerContext>(
            nameof(Categoriser), indexerContext with {CategoriserOperationId = context.NewGuid()});
        logger.LogInformation("{nameofCategoriser} complete.", nameof(Categoriser));

        indexerContext = await context.CallActivityAsync<IndexerContext>(nameof(YouTubeThumbnailRecheck), indexerContext);
        logger.LogInformation("{nameofYouTubeThumbnailRecheck} complete.", nameof(YouTubeThumbnailRecheck));

        var postingSkipped = failedPasses.Count > 0;
        if (postingSkipped)
        {
            // After a failed Indexer pass only the homepage publish runs. Reddit (Poster), X (Tweet) and Bluesky are
            // skipped entirely, exactly as before this change, because posting after an indexer failure was never
            // approved (AGENTS.md: no posting behaviour change during cost-reduction work).
            logger.LogWarning(
                IndexerPassFailedMessagePrefix +
                " posting-skipped hour-utc='{HourUtc}' failed-passes='{FailedPasses}' skipped='Poster,Tweet,Bluesky'.",
                hourUtc, string.Join(",", failedPasses));
        }
        else
        {
            indexerContext = await context.CallActivityAsync<IndexerContext>(
                nameof(Poster), indexerContext with {PosterOperationId = context.NewGuid()});
            logger.LogInformation("{nameofPoster} complete.", nameof(Poster));
        }

        indexerContext = await context.CallActivityAsync<IndexerContext>(
            nameof(Publisher), indexerContext with {PublisherOperationId = context.NewGuid()});
        logger.LogInformation("{nameofPublisher} complete.", nameof(Publisher));

        if (!postingSkipped)
        {
            indexerContext = await context.CallActivityAsync<IndexerContext>(
                nameof(Tweet), indexerContext with {TweetOperationId = context.NewGuid()});
            logger.LogInformation("{nameofTweet} complete.", nameof(Tweet));

            indexerContext = await context.CallActivityAsync<IndexerContext>(
                nameof(Bluesky), indexerContext with {BlueskyOperationId = context.NewGuid()});
            logger.LogInformation("{nameofBluesky} complete. All tasks complete.", nameof(Bluesky));
        }

        if (failedPasses.Count > 0)
        {
            throw new IndexerPassesFailedException(failedPasses);
        }

        return indexerContext;
    }

    /// <summary>Stable log prefix for an Indexer activity failure the orchestration continued past.</summary>
    public const string IndexerPassFailedMessagePrefix = "HourlyOrchestration indexer-pass-failed";

    /// <summary>
    /// After a failed pass the hour's YouTube/Spotify state is unknown: flag both as errored so the thumbnail re-check
    /// makes no YouTube call (Poster, Tweet and Bluesky do not run at all after a failed pass).
    /// </summary>
    public static IndexerContext WithPostingHeldBack(IndexerContext indexerContext) =>
        indexerContext with
        {
            Success = false,
            YouTubeError = true,
            SpotifyError = true
        };

    private static void LogIndexerPassFailed(
        ILogger logger,
        int pass,
        int hourUtc,
        IndexerContext indexerContext,
        TaskFailedException ex) =>
        logger.LogError(
            IndexerPassFailedMessagePrefix +
            " pass='{Pass}' hour-utc='{HourUtc}' operation-id='{OperationId}' error-type='{ErrorType}' error='{ErrorMessage}'. Continuing to publish.",
            pass, hourUtc, indexerContext.IndexerPassOperationIds?[pass - 1],
            ex.FailureDetails?.ErrorType, ex.FailureDetails?.ErrorMessage);

    private static void LogIndexerPassComplete(
        ILogger logger,
        int pass,
        int hourUtc,
        IndexerContext indexerContext)
    {
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

        logger.LogInformation("{nameofIndexer} complete - Pass {pass}.", nameof(global::Indexer.Activities.Indexer), pass);
    }
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
