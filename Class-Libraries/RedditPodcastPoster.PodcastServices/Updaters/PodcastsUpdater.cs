using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Providers;

namespace RedditPodcastPoster.PodcastServices.Updaters;

public class PodcastsUpdater(
    IIndexablePodcastIdProvider indexablePodcastIdProvider,
    IPodcastUpdater podcastUpdater,
    IPodcastRepository podcastRepository,
    IPodcastPassApiCache passApiCache,
    IOptions<PodcastUpdateBudgetOptions> budgetOptions,
    IOptions<PodcastIndexingStageLogOptions> stageLogOptions,
    PodcastUpdateBudgetCutTracker budgetCutTracker,
    TimeProvider timeProvider,
    ILogger<PodcastsUpdater> logger)
    : IPodcastsUpdater
{
    /// <summary>Stable log prefix for a podcast cut off by its time budget (App Insights keys off it).</summary>
    public const string BudgetExceededMessagePrefix = "IndexerPodcastBudgetExceeded";

    /// <summary>Stable log prefix when an abandoned update finally finishes in the background.</summary>
    public const string AbandonedCompletedMessagePrefix = "IndexerPodcastAbandonedCompleted";

    private readonly PodcastUpdateBudgetOptions _budget = budgetOptions.Value ?? new PodcastUpdateBudgetOptions();

    private readonly PodcastIndexingStageLogOptions _stageLogOptions =
        stageLogOptions.Value ?? new PodcastIndexingStageLogOptions();

    public async Task<bool> UpdatePodcasts(Guid[] podcastIds, IndexingContext indexingContext)
    {
        var success = true;
        var batchBypassState = new IndexingContextExtensions.PodcastBatchBypassState(
            indexingContext.SkipYouTubeUrlResolving,
            indexingContext.SkipSpotifyUrlResolving);
        var youtubeEnabled = !indexingContext.SkipYouTubeUrlResolving;
        var youtubeAuthorityInBatch = 0;
        var youtubeAuthorityIndexedWithYouTubePass = 0;
        var youtubeAuthorityBypassed = 0;

        var podcasts = await Task.WhenAll(podcastIds.Select(async podcastId =>
            (Id: podcastId, Podcast: await podcastRepository.GetPodcast(podcastId))));
        var orderedPodcasts = podcasts
            .OrderByDescending(x => x.Podcast?.DependsOnYouTubeForEpisodeDiscovery() == true)
            .ThenBy(x => x.Id)
            .ToArray();

        // A pass that allows expensive queries (00 UTC primary pass) may legitimately walk whole catalogues.
        var expensivePass = !indexingContext.SkipExpensiveYouTubeQueries || !indexingContext.SkipExpensiveSpotifyQueries;
        logger.LogInformation("{nameofUpdatePodcasts} Indexing Starting.", nameof(UpdatePodcasts));
        foreach (var (podcastId, podcast) in orderedPodcasts)
        {
            var dependsOnYouTubeForDiscovery = podcast?.DependsOnYouTubeForEpisodeDiscovery() == true;
            if (dependsOnYouTubeForDiscovery)
            {
                youtubeAuthorityInBatch++;
            }

            var performAutoIndex = podcast != null &&
                                   podcast.Removed != true &&
                                   (podcast.IndexAllEpisodes ||
                                    !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex));
            if (performAutoIndex)
            {
                // performAutoIndex requires podcast != null; capture for catch (flow analysis does not carry in).
                var podcastToUpdate = podcast!;
                var stageLog = new PodcastIndexingStageLog(
                    podcastToUpdate.Id, podcastToUpdate.Name, logger, timeProvider, _stageLogOptions);
                var budget = _budget.BudgetFor(expensivePass);
                Task<IndexPodcastResult>? updateTask = null;
                using var budgetCts = budget.HasValue
                    ? new CancellationTokenSource(budget.Value, timeProvider)
                    : new CancellationTokenSource();
                try
                {
                    stageLog.Stage("start");
                    var podcastIndexingContext = indexingContext.ForPodcastUpdate() with
                    {
                        CancellationToken = budgetCts.Token,
                        StageReporter = stageLog
                    };
                    updateTask = podcastUpdater.Update(podcastToUpdate, false, podcastIndexingContext);
                    var result = budget.HasValue
                        ? await updateTask.WaitAsync(budget.Value + _budget.AbandonGrace, timeProvider)
                        : await updateTask;
                    stageLog.Stage("complete");
                    budgetCutTracker.RecordCompleted(podcastToUpdate.Id);
                    batchBypassState = indexingContext.AbsorbPodcastPass(
                        podcastIndexingContext,
                        batchBypassState);
                    var resultReport = result.ToString();
                    if (!result.Success)
                    {
                        logger.LogError("{report}",resultReport);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(resultReport))
                        {
                            logger.LogInformation("{result}",result.ToString());
                        }
                    }

                    if (dependsOnYouTubeForDiscovery)
                    {
                        var youtubeEnriched = result.EnrichmentResult.UpdatedEpisodes
                            .Count(e => e.EnrichmentContext.YouTubeUrlUpdated ||
                                        e.EnrichmentContext.YouTubeIdUpdated);

                        logger.LogWarning(
                            "YouTubeAuthorityPodcastAudit podcast-id='{PodcastId}' podcast-name='{PodcastName}' youtube-enabled='{YouTubeEnabled}' youtube-bypassed='{YouTubeBypassed}' episodes-added='{EpisodesAdded}' youtube-enriched='{YouTubeEnriched}'",
                            podcastToUpdate.Id, podcastToUpdate.Name, youtubeEnabled, result.YouTubeBypassed,
                            result.MergeResult.AddedEpisodes.Count, youtubeEnriched);

                        if (youtubeEnabled)
                        {
                            youtubeAuthorityIndexedWithYouTubePass++;
                            if (result.YouTubeBypassed)
                            {
                                youtubeAuthorityBypassed++;
                            }
                        }
                    }

                    success &= result.Success;
                }
                catch (Exception ex) when (budgetCts.IsCancellationRequested &&
                                           ex is OperationCanceledException or TimeoutException)
                {
                    // TimeoutException: the update ignored its cancelled token and was abandoned after the grace period.
                    var abandoned = ex is TimeoutException;
                    var consecutiveCuts = budgetCutTracker.RecordCut(podcastToUpdate.Id);
                    logger.LogError(
                        BudgetExceededMessagePrefix +
                        " podcast-id='{PodcastId}' podcast-name='{PodcastName}' stage='{Stage}' detail='{Detail}' elapsed-ms='{ElapsedMs}' budget-seconds='{BudgetSeconds}' expensive-pass='{ExpensivePass}' abandoned='{Abandoned}' consecutive-cuts='{ConsecutiveCuts}'. Continuing with the next podcast.",
                        podcastToUpdate.Id, podcastToUpdate.Name, stageLog.CurrentStage,
                        stageLog.CurrentStageDetail ?? string.Empty, stageLog.ElapsedMilliseconds,
                        (int) budget!.Value.TotalSeconds, expensivePass, abandoned, consecutiveCuts);
                    if (abandoned && updateTask != null)
                    {
                        ObserveAbandonedUpdate(updateTask, podcastToUpdate, stageLog);
                    }

                    success = false;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failure updating podcast with id '{podcastId}' and name '{podcastName}'.",
                        podcastToUpdate.Id, podcastToUpdate.Name);
                    success = false;
                }
                finally
                {
                    passApiCache.Clear();
                }
            }
            else if (dependsOnYouTubeForDiscovery)
            {
                logger.LogWarning(
                    "YouTubeAuthorityPodcastAudit podcast-id='{PodcastId}' podcast-name='{PodcastName}' youtube-enabled='{YouTubeEnabled}' indexed='False'",
                    podcast!.Id, podcast.Name, youtubeEnabled);
            }
        }

        if (youtubeAuthorityInBatch > 0)
        {
            logger.LogWarning(
                "YouTubeAuthorityIndexingAudit youtube-enabled='{YouTubeEnabled}' in-batch='{InBatch}' indexed-with-youtube-pass='{IndexedWithYouTubePass}' youtube-bypassed='{YouTubeBypassed}'",
                youtubeEnabled, youtubeAuthorityInBatch, youtubeAuthorityIndexedWithYouTubePass,
                youtubeAuthorityBypassed);
        }

        indexingContext.ApplyBatchBypassRollup(batchBypassState);

        logger.LogInformation("{nameofUpdatePodcasts} Indexing complete.", nameof(UpdatePodcasts));
        return success;
    }

    public async Task<bool> UpdatePodcasts(IndexingContext indexingContext)
    {
        var podcastIds = indexablePodcastIdProvider.GetIndexablePodcastIds();
        return await UpdatePodcasts(await podcastIds.ToArrayAsync(), indexingContext);
    }

    /// <summary>
    /// An abandoned update keeps running in the background. Observe it so its outcome (including a late fault or a write
    /// that started before cancellation) is logged instead of becoming an unobserved task exception. The update's own
    /// cancelled token stops it before any further Cosmos write (<see cref="PodcastUpdater"/> checks it before each one).
    /// </summary>
    private void ObserveAbandonedUpdate(Task<IndexPodcastResult> updateTask, Podcast podcast,
        PodcastIndexingStageLog stageLog)
    {
        updateTask.ContinueWith(task =>
            {
                var error = task.Exception?.GetBaseException();
                logger.LogWarning(
                    AbandonedCompletedMessagePrefix +
                    " podcast-id='{PodcastId}' podcast-name='{PodcastName}' status='{Status}' last-stage='{Stage}' elapsed-ms='{ElapsedMs}' error-type='{ErrorType}'",
                    podcast.Id, podcast.Name, task.Status, stageLog.CurrentStage, stageLog.ElapsedMilliseconds,
                    error?.GetType().Name ?? string.Empty);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
