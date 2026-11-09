using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.Persistence.Abstractions.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;

namespace RedditPodcastPoster.Indexing.Services;

public class Indexer(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IPodcastUpdater podcastUpdater,
    ISubjectEnricher subjectEnricher,
    ISubjectEnrichmentOptionsFactory subjectEnrichmentOptionsFactory,
    IEpisodeGuestEnricher guestEnricher,
    IParentIndexingLockStore indexingLockStore,
    IOptions<PodcastUpdateBudgetOptions> budgetOptions,
    TimeProvider timeProvider,
    ILogger<Indexer> logger
) : IIndexer
{
    /// <summary>Stable log prefix when on-demand indexing is cut by its time budget (App Insights keys off it).</summary>
    public const string BudgetExceededMessagePrefix = "IndexerOnDemandBudgetExceeded";

    /// <summary>Stable log prefix when an abandoned on-demand update finally finishes in the background.</summary>
    public const string AbandonedCompletedMessagePrefix = "IndexerOnDemandAbandonedCompleted";

    /// <summary>
    /// Stable log prefix when on-demand answers while its abandoned update is still running after the drain
    /// (the update may then fail once the request scope is disposed).
    /// </summary>
    public const string AbandonedOutlivesRequestMessagePrefix = "IndexerOnDemandAbandonedOutlivesRequest";

    /// <summary>
    /// A marker with no heartbeat for this long belongs to an index that died.
    /// At least on-demand budget (<see cref="PodcastUpdateBudgetOptions.MaxOnDemandBudgetSeconds"/>) plus abandon
    /// grace and drain (~195s); five minutes covers that with margin.
    /// </summary>
    public static readonly TimeSpan IndexingLockStaleAfter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often the holder renews its marker while it works. Zero disables heartbeats: every holder must stay
    /// budget-capped under <see cref="IndexingLockStaleAfter"/> (on-demand ≤ ~195s with grace + drain; hourly uses
    /// <see cref="PodcastUpdateBudgetOptions.BudgetFor"/>). Heartbeats are not a substitute for that cap.
    /// </summary>
    public static readonly TimeSpan IndexingLockHeartbeatInterval = TimeSpan.Zero;

    private readonly PodcastUpdateBudgetOptions _budget = budgetOptions.Value ?? new PodcastUpdateBudgetOptions();

    public async Task<IndexResponse> Index(string podcastName, IndexingContext indexingContext)
    {
        var podcasts = await FindPodcastsByName(podcastName);

        if (!podcasts.Any())
        {
            logger.LogWarning(
                "No podcast found for name '{PodcastName}'.", podcastName);
            return new IndexResponse(IndexStatus.NotFound);
        }

        var canIndex = podcasts.Where(x =>
            !(x.Removed.HasValue && x.Removed.Value) &&
            (x.IndexAllEpisodes || !string.IsNullOrWhiteSpace(x.EpisodeIncludeTitleRegex))).ToArray();
        if (!canIndex.Any() || canIndex.Count() > 1)
        {
            if (podcasts.Count == 1 && !canIndex.Any())
            {
                canIndex = [podcasts.Single()];
            }
            else
            {
                return new IndexResponse(IndexStatus.NotPerformed);
            }
        }

        var podcast = canIndex.Single();
        return await Index(podcast.Id, indexingContext);
    }

    public async Task<IndexResponse> Index(Guid podcastId, IndexingContext indexingContext, bool forceIndex = false)
    {
        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            return new IndexResponse(IndexStatus.NotFound);
        }

        if (podcast.Removed == true)
        {
            logger.LogWarning("Podcast '{PodcastName}' with id '{PodcastId}' is removed and will not be indexed.",
                podcast.Name,
                podcast.Id);
            return new IndexResponse(IndexStatus.NotPerformed);
        }

        // Only include shorts when the podcast explicitly bypasses short-episode checking.
        // Unset (null) or false must skip shorts — do not use `!flag ?? false` (operator precedence).
        indexingContext = indexingContext with
        {
            SkipShortEpisodes = podcast.BypassShortEpisodeChecking != true
        };

        IndexStatus status;
        IndexedEpisode[]? updatedEpisodes = null;

        var performAutoIndex = podcast.IndexAllEpisodes ||
                               !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex);
        var enrichOnly = false;
        if (!performAutoIndex)
        {
            var hasEpisodesToEnrich = await HasEpisodesAwaitingEnrichment(podcastId, indexingContext);
            if (hasEpisodesToEnrich)
            {
                enrichOnly = true;
            }
        }

        if (performAutoIndex || enrichOnly || forceIndex)
        {
            var lease = await ParentIndexingLease.TryStart(
                indexingLockStore,
                timeProvider,
                podcastId,
                IndexingLockStaleAfter,
                IndexingLockHeartbeatInterval,
                ParentIndexingLock.SourceOnDemand,
                logger);
            if (lease is null)
            {
                logger.LogWarning(
                    "IndexerParentIndexingAlreadyRunning podcast-id='{PodcastId}' podcast-name='{PodcastName}' source='{Source}'.",
                    podcastId, podcast.Name, ParentIndexingLock.SourceOnDemand);
                return new IndexResponse(IndexStatus.AlreadyRunning);
            }

            await using (lease)
            {
                logger.LogInformation("Indexing podcast '{podcastName}' with podcast-id '{podcastId}'.", podcast.Name,
                    podcastId);

                var budget = _budget.OnDemandBudget;
                Task<IndexPodcastResult>? updateTask = null;
                using var budgetCts = new CancellationTokenSource(budget, timeProvider);
                try
                {
                    // Copy so the update's bypass flags stay on this run; the budget token travels on the run scope.
                    var onDemandContext = indexingContext with { };
                    using (PodcastRunScope.Enter(new PodcastRunScope(budgetCts.Token)))
                    {
                        updateTask = podcastUpdater.Update(podcast, enrichOnly, onDemandContext);
                    }

                    var results = await updateTask.WaitAsync(budget + _budget.OnDemandAbandonGrace, timeProvider);

                    updatedEpisodes = results.MergeResult.AddedEpisodes
                        .Select(x =>
                            new IndexedEpisode(
                                x,
                                EpisodeServicePresence.HasUrl(x, ServiceKeys.Spotify),
                                EpisodeServicePresence.HasUrl(x, ServiceKeys.Apple),
                                EpisodeServicePresence.HasUrl(x, ServiceKeys.YouTube)))
                        .Concat(results.EnrichmentResult.UpdatedEpisodes.Select(x =>
                            new IndexedEpisode(
                                x.Episode,
                                x.EnrichmentContext.SpotifyUrlUpdated,
                                x.EnrichmentContext.AppleUrlUpdated,
                                x.EnrichmentContext.YouTubeUrlUpdated)))
                        // Elimination marks Removed in Cosmos; include those IDs so search push deletes them.
                        .Concat(results.FilterResult.FilteredEpisodes.Select(x =>
                            new IndexedEpisode(
                                x.Episode,
                                EpisodeServicePresence.HasUrl(x.Episode, ServiceKeys.Spotify),
                                EpisodeServicePresence.HasUrl(x.Episode, ServiceKeys.Apple),
                                EpisodeServicePresence.HasUrl(x.Episode, ServiceKeys.YouTube))))
                        .Distinct()
                        .ToArray();

                    var resultsMessage = results.ToString();
                    if (results.MergeResult.FailedEpisodes.Any() ||
                        (results.SpotifyBypassed && !onDemandContext.SkipSpotifyUrlResolving) ||
                        (results.YouTubeBypassed && !onDemandContext.SkipYouTubeUrlResolving))
                    {
                        logger.LogError(resultsMessage);
                    }
                    else
                    {
                        logger.LogInformation(resultsMessage);
                    }

                    foreach (var indexedEpisode in updatedEpisodes.Where(x => !x.Episode.IsRemoved()))
                    {
                        var subjectsResult = await subjectEnricher.EnrichSubjects(
                            indexedEpisode.Episode,
                            await subjectEnrichmentOptionsFactory.CreateAsync(podcast, indexedEpisode.Episode));

                        var guestsResult = await guestEnricher.EnrichGuests(indexedEpisode.Episode);

                        var result = updatedEpisodes.FirstOrDefault(x => x.Episode.Id == indexedEpisode.Episode.Id);
                        result?.Subjects = subjectsResult.Additions;

                        if (subjectsResult.Additions.Any() || subjectsResult.Removals.Any() ||
                            guestsResult.Additions.Any())
                        {
                            await episodeRepository.Save(indexedEpisode.Episode);
                        }
                    }

                    status = IndexStatus.Performed;
                }
                catch (Exception ex) when (budgetCts.IsCancellationRequested &&
                                           ex is OperationCanceledException or TimeoutException)
                {
                    var abandoned = ex is TimeoutException;
                    logger.LogError(
                        BudgetExceededMessagePrefix +
                        " podcast-id='{PodcastId}' podcast-name='{PodcastName}' budget-seconds='{BudgetSeconds}' abandoned='{Abandoned}'.",
                        podcastId, podcast.Name, (int) budget.TotalSeconds, abandoned);
                    if (abandoned && updateTask != null)
                    {
                        ObserveAbandonedUpdate(updateTask, podcast);
                        await DrainAbandonedUpdate(updateTask, _budget.OnDemandAbandonDrain, podcast);
                    }

                    status = IndexStatus.TimedOut;
                }
            }
        }
        else
        {
            logger.LogWarning(
                "Podcast '{podcastName}' with id '{podcastId}' ignored. index-all-episodes '{podcastIndexAllEpisodes}', episode-include-title-regex: '{podcastEpisodeIncludeTitleRegex}'.",
                podcast.Name, podcast.Id, podcast.IndexAllEpisodes, podcast.EpisodeIncludeTitleRegex);

            status = IndexStatus.NotPerformed;
        }

        updatedEpisodes = Collapse(updatedEpisodes);

        return new IndexResponse(status, updatedEpisodes);
    }

    private async Task<List<Podcast>> FindPodcastsByName(string podcastName)
    {
        var podcasts = await podcastRepository.GetAllBy(x => x.Name == podcastName).ToListAsync();
        if (podcasts.Count == 0)
        {
            var lowerName = podcastName.ToLower();
            podcasts = await podcastRepository.GetAllBy(x => x.Name.ToLower() == lowerName).ToListAsync();
        }

        return podcasts;
    }

    private async Task<bool> HasEpisodesAwaitingEnrichment(Guid podcastId, IndexingContext indexingContext)
    {
        // Use IEpisodeRepository instead of legacy repository method
        var episodes = await episodeRepository.GetByPodcastId(podcastId)
            .Where(x => x.ReleaseSort >= indexingContext.ReleasedSince)
            .ToListAsync();

        return episodes.Any();
    }

    /// <summary>
    /// An on-demand index runs inside the HTTP request's DI scope. Give an abandoned update a short, bounded time to
    /// stop so it finishes while scoped repositories are still alive — then release the lease.
    /// </summary>
    private async Task DrainAbandonedUpdate(Task updateTask, TimeSpan drain, Podcast podcast)
    {
        if (drain <= TimeSpan.Zero || updateTask.IsCompleted)
        {
            return;
        }

        await Task.WhenAny(updateTask, Task.Delay(drain, timeProvider));
        if (!updateTask.IsCompleted)
        {
            logger.LogWarning(
                AbandonedOutlivesRequestMessagePrefix +
                " podcast-id='{PodcastId}' podcast-name='{PodcastName}' drain-seconds='{DrainSeconds}'. The abandoned update is still running after the on-demand drain and may fail once the request scope is disposed.",
                podcast.Id, podcast.Name, (int) drain.TotalSeconds);
        }
    }

    private void ObserveAbandonedUpdate(Task<IndexPodcastResult> updateTask, Podcast podcast)
    {
        updateTask.ContinueWith(task =>
            {
                var error = task.Exception?.GetBaseException();
                logger.LogWarning(
                    AbandonedCompletedMessagePrefix +
                    " podcast-id='{PodcastId}' podcast-name='{PodcastName}' status='{Status}' error-type='{ErrorType}'",
                    podcast.Id, podcast.Name, task.Status, error?.GetType().Name ?? string.Empty);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static IndexedEpisode[]? Collapse(IndexedEpisode[]? episodes)
    {
        if (episodes == null)
        {
            return null;
        }

        var results = new List<IndexedEpisode>();
        var groupedEpisodes = episodes.GroupBy(x => x.Episode.Id);
        foreach (var groupedEpisode in groupedEpisodes)
        {
            var items = groupedEpisode.ToList();
            var result = new IndexedEpisode(
                items.First().Episode,
                items.Any(x => x.Spotify),
                items.Any(x => x.Apple),
                items.Any(x => x.YouTube));
            var subjects = items.SelectMany(x => x.Subjects).Distinct();
            result.Subjects = subjects.ToArray();
            results.Add(result);
        }

        return results.ToArray();
    }
}
