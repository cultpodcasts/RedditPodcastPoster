using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching; // pragma: allowlist secret
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Apple.Enrichers;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.PodcastServices.Updaters;

/// <summary>
/// Indexes TvShow and NewsOrganisation parents that kept auto-index discovery settings
/// (<see cref="CatalogueIndexingSource"/>) from their migrated podcast. Each parent becomes a
/// transient <see cref="Podcast"/>; its playables in the release window are projected to
/// <see cref="Episode"/> (provider ids derived from <c>services</c> URLs, since playables do
/// not store them). Discovery, merge, the minimum-duration rule and elimination terms are the
/// same as <see cref="PodcastUpdater"/>. New items are saved as TvShowEpisode / NewsReport;
/// merged and term-removed items update the stored playable. The parent's stored ignore-all-episodes, bypass-short
/// and minimum-duration settings apply as for a podcast (a news organisation is saved with bypass-short on when it
/// is created, see <see cref="CatalogueIndexingSource.ApplyNewsOrganisationCreationDefaults"/>). When the parent is flagged
/// <see cref="CatalogueIndexingSource.AlwaysPromoteAsHero"/>, new playables are appended to the homepage hero list
/// after they are saved (same <see cref="HeroAutoPromoteSelector"/> rule as <see cref="PodcastUpdater"/>).
/// <para>
/// Spotify / Apple catch-up: after discovery, playables in the window that lack a Spotify link (parent has a Spotify
/// show id) or an Apple link (parent has an Apple podcast id) are offered to the same
/// <see cref="ISpotifyEpisodeEnricher"/> / <see cref="IAppleEpisodeEnricher"/> that <see cref="PodcastUpdater"/> runs
/// through <c>IPodcastServicesEpisodeEnricher</c>, on every pass. Discovery alone cannot fill them: Spotify discovery
/// only runs on even hours (<c>IndexingStrategy.IndexSpotify</c>) and the hourly selector puts parent batches 3-4 on
/// odd hours, and Apple discovery is skipped whenever Spotify handled the pass. The enrichers work on a copy of each
/// item and only its service links and ids are copied back, so title, description, release and length stay as stored
/// or as merged this pass. <see cref="EnrichmentRequest.Episodes"/> is the window plus the parent's stored items that
/// already hold a Spotify or Apple link (read once per pass, only when there is a candidate), so the enrichers' rule that
/// an id already owned by another item is never assigned again also covers links held outside the window. The
/// catch-up is bounded so it cannot eat the per-parent budget that discovery and the save need: at most
/// <see cref="AudioCatchUpMaxCandidatesPerPass"/> items per pass (newest first, then longest first so a full episode
/// claims its link before a shorter item of the same title when both are looked up). A shorter item is kept out of a
/// service while a strictly longer same-title item in the window still lacks that service and is not in this pass's
/// capped set, including when that longer item is outside the recency window, outside the newest few, or older than
/// this pass's release cutoff while still inside the matcher's 14-day acceptance (one release-filtered read; the
/// provider cutoff and the parent's whole history stay as they are, and #1161 stays deferred). Only items released
/// in the last <see cref="AudioCatchUpRecency"/> are looked up, no item
/// shorter than <see cref="AudioCatchUpMinimumLength"/> (a clip is not looked up), and a soft stop once
/// <see cref="AudioCatchUpBudgetShare"/> of the pass budget has elapsed. A stopped or failed catch-up never fails the
/// pass. A Spotify rate limit hit during the catch-up does not mark the pass as bypassed (the bypass check uses the skip
/// flags as discovery left them), so <c>lastIndexed</c> still moves when discovery completed.
/// YouTube enrichment is not run for these kinds (YouTube discovery already covers them and quota is scarce).
/// </para>
/// <para>
/// The parent itself is never upserted: only the Indexer-owned fields a pass changed
/// (<c>lastIndexed</c>, <c>latestReleased</c>, changed <c>indexing</c> members) are patched via
/// <see cref="CatalogueParentIndexerPatch"/>, so curator edits, renames and removals saved during the
/// pass survive, and an unchanged parent costs no write.
/// </para>
/// <para>
/// Each parent runs under the same per-podcast time budget as <see cref="PodcastsUpdater"/>
/// (<see cref="PodcastUpdateBudgetOptions"/>, #1070): its <see cref="IndexingContext.CancellationToken"/> is cancelled when
/// the budget elapses, every Cosmos write re-checks the token, and an update that ignores cancellation is abandoned
/// after the grace period. A slow news organisation therefore cannot hang the indexer activity. Stage lines use
/// <see cref="PodcastIndexingStageLog"/> (prefix <c>IndexerPodcastStage</c>, the parent id in <c>podcast-id</c>).
/// </para>
/// </summary>
public class CatalogueParentsUpdater(
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IEpisodeProvider episodeProvider,
    IEpisodeMerger episodeMerger,
    IPodcastFilter podcastFilter,
    IAsyncInstance<IEliminationTermsProvider> eliminationTermsProviderInstance,
    IOptions<PostingCriteria> postingCriteria,
    IPodcastPassApiCache passApiCache,
    IOptions<PodcastUpdateBudgetOptions> budgetOptions,
    IOptions<PodcastIndexingStageLogOptions> stageLogOptions,
    PodcastUpdateBudgetCutTracker budgetCutTracker,
    TimeProvider timeProvider,
    IHeroEpisodePromoter heroEpisodePromoter,
    ISpotifyEpisodeEnricher spotifyEpisodeEnricher,
    IAppleEpisodeEnricher appleEpisodeEnricher,
    ILogger<CatalogueParentsUpdater> logger) : ICatalogueParentsUpdater
{
    /// <summary>Stable log prefix for a parent cut off by its time budget (App Insights keys off it).</summary>
    public const string BudgetExceededMessagePrefix = "IndexerCatalogueParentBudgetExceeded";

    /// <summary>
    /// Stable log prefix for a parent skipped because the podcast it was migrated from (same id) still exists: a
    /// half-finished CatalogueMigrate move (App Insights keys off it).
    /// </summary>
    public const string SourcePodcastExistsMessagePrefix = "IndexerCatalogueParentSourcePodcastExists";

    /// <summary>Stable log prefix when an abandoned parent update finally finishes in the background.</summary>
    public const string AbandonedCompletedMessagePrefix = "IndexerCatalogueParentAbandonedCompleted";

    /// <summary>
    /// Stable log prefix when an on-demand index answers while its abandoned update is still running after the drain
    /// (the update may then fail once the request scope is disposed).
    /// </summary>
    public const string AbandonedOutlivesRequestMessagePrefix = "IndexerCatalogueParentAbandonedOutlivesRequest";

    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    private readonly PodcastUpdateBudgetOptions _budget = budgetOptions.Value ?? new PodcastUpdateBudgetOptions();

    private readonly PodcastIndexingStageLogOptions _stageLogOptions =
        stageLogOptions.Value ?? new PodcastIndexingStageLogOptions();

    public async Task<Guid[]> GetIndexableParentIds()
    {
        var tvShows = await tvShowRepository
            .GetAllBy(x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) &&
                           x.Indexing != null &&
                           (x.Indexing.IndexAllEpisodes ||
                            (x.Indexing.EpisodeIncludeTitleRegex != null && x.Indexing.EpisodeIncludeTitleRegex != "")))
            .ToListAsync();
        var organisations = await newsOrganisationRepository
            .GetAllBy(x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) &&
                           x.Indexing != null &&
                           (x.Indexing.IndexAllEpisodes ||
                            (x.Indexing.EpisodeIncludeTitleRegex != null && x.Indexing.EpisodeIncludeTitleRegex != "")))
            .ToListAsync();

        return tvShows.Where(IsIndexable).Select(x => x.Id)
            .Concat(organisations.Where(IsIndexable).Select(x => x.Id))
            .Distinct()
            .Order()
            .ToArray();
    }

    public async Task<bool> UpdateParents(Guid[] parentIds, IndexingContext indexingContext)
    {
        ArgumentNullException.ThrowIfNull(parentIds);
        if (!indexingContext.ReleasedSince.HasValue)
        {
            throw new InvalidOperationException("Cannot index catalogue parents without a released-since.");
        }

        // A pass that allows expensive queries (00 UTC primary pass) gets the scaled budget, as for podcasts.
        var expensivePass = !indexingContext.SkipExpensiveYouTubeQueries || !indexingContext.SkipExpensiveSpotifyQueries;
        var success = true;
        foreach (var parentId in parentIds)
        {
            try
            {
                var tvShow = await tvShowRepository.GetTvShow(parentId);
                if (tvShow is not null)
                {
                    if (IsIndexable(tvShow) && !await SourcePodcastStillExists("TvShow", tvShow))
                    {
                        success &= await IndexWithinBudget("TvShow", tvShow,
                            context => IndexTvShow(tvShow, context), indexingContext, expensivePass) is not null;
                    }

                    continue;
                }

                var organisation = await newsOrganisationRepository.GetNewsOrganisation(parentId);
                if (organisation is not null && IsIndexable(organisation) &&
                    !await SourcePodcastStillExists("NewsOrganisation", organisation))
                {
                    success &= await IndexWithinBudget("NewsOrganisation", organisation,
                        context => IndexNewsOrganisation(organisation, context), indexingContext, expensivePass) is not null;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure indexing catalogue parent with id '{ParentId}'.", parentId);
                success = false;
            }
            finally
            {
                passApiCache.Clear();
            }
        }

        return success;
    }

    public async Task<CatalogueParentIndexResult> IndexParent(Publisher parent, IndexingContext indexingContext)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (!indexingContext.ReleasedSince.HasValue)
        {
            throw new InvalidOperationException("Cannot index catalogue parents without a released-since.");
        }

        try
        {
            return parent switch
            {
                TvShow tvShow => await IndexOnDemand("TvShow", tvShow,
                    context => IndexTvShow(tvShow, context), indexingContext),
                NewsOrganisation organisation => await IndexOnDemand("NewsOrganisation", organisation,
                    context => IndexNewsOrganisation(organisation, context), indexingContext),
                _ => throw new ArgumentException(
                    $"Only a TV show or a news organisation can be indexed, not '{parent.GetType().Name}'.",
                    nameof(parent))
            };
        }
        finally
        {
            // After IndexOnDemand, which waits (bounded) for an abandoned update to settle first.
            passApiCache.Clear();
        }
    }

    private async Task<CatalogueParentIndexResult> IndexOnDemand(
        string kind,
        Publisher parent,
        Func<IndexingContext, Task<IReadOnlyList<Playable>>> index,
        IndexingContext indexingContext)
    {
        if (!IsIndexable(parent))
        {
            logger.LogWarning(
                "On-demand index: catalogue parent kind='{Kind}' id='{ParentId}' name='{ParentName}' is removed or not auto-indexed.",
                kind, parent.Id, parent.Name);
            return new CatalogueParentIndexResult(CatalogueParentIndexStatus.NotIndexable);
        }

        if (await SourcePodcastStillExists(kind, parent))
        {
            return new CatalogueParentIndexResult(CatalogueParentIndexStatus.SourcePodcastStillExists);
        }

        var expensivePass = !indexingContext.SkipExpensiveYouTubeQueries || !indexingContext.SkipExpensiveSpotifyQueries;
        var changed = await IndexWithinBudget(kind, parent, index, indexingContext, expensivePass,
            new BudgetPlan(_budget.OnDemandBudget, _budget.OnDemandAbandonGrace, _budget.OnDemandAbandonDrain));
        return changed is null
            ? new CatalogueParentIndexResult(CatalogueParentIndexStatus.TimedOut)
            : new CatalogueParentIndexResult(CatalogueParentIndexStatus.Indexed, changed);
    }

    /// <summary>
    /// Runs one parent's index under its time budget. Returns the saved playables, or null when the budget cut it off;
    /// other failures propagate to the caller's per-parent catch. The hourly pass uses <see cref="HourlyPlan"/>; an
    /// on-demand index passes its own, always-bounded plan.
    /// </summary>
    private async Task<IReadOnlyList<Playable>?> IndexWithinBudget(
        string kind,
        Publisher parent,
        Func<IndexingContext, Task<IReadOnlyList<Playable>>> index,
        IndexingContext indexingContext,
        bool expensivePass,
        BudgetPlan? plan = null)
    {
        plan ??= HourlyPlan(expensivePass);
        var budget = plan.Budget;
        var stageLog = new PodcastIndexingStageLog(parent.Id, parent.Name, logger, timeProvider, _stageLogOptions);
        Task<IReadOnlyList<Playable>>? updateTask = null;
        using var budgetCts = budget.HasValue
            ? new CancellationTokenSource(budget.Value, timeProvider)
            : new CancellationTokenSource();
        try
        {
            stageLog.Stage("start", $"catalogue-parent-kind={kind}");
            var parentContext = indexingContext.ForPodcastUpdate() with
            {
                CancellationToken = budgetCts.Token,
                StageReporter = stageLog
            };
            updateTask = index(parentContext);
            var changed = budget.HasValue
                ? await updateTask.WaitAsync(budget.Value + plan.AbandonGrace, timeProvider)
                : await updateTask;

            stageLog.Stage("complete");
            budgetCutTracker.RecordCompleted(parent.Id);
            return changed;
        }
        catch (Exception ex) when (budgetCts.IsCancellationRequested &&
                                   ex is OperationCanceledException or TimeoutException)
        {
            // TimeoutException: the update ignored its cancelled token and was abandoned after the grace period.
            var abandoned = ex is TimeoutException;
            var consecutiveCuts = budgetCutTracker.RecordCut(parent.Id);
            logger.LogError(
                BudgetExceededMessagePrefix +
                " kind='{Kind}' parent-id='{ParentId}' parent-name='{ParentName}' stage='{Stage}' detail='{Detail}' elapsed-ms='{ElapsedMs}' budget-seconds='{BudgetSeconds}' expensive-pass='{ExpensivePass}' abandoned='{Abandoned}' consecutive-cuts='{ConsecutiveCuts}'. Continuing with the next parent.",
                kind, parent.Id, parent.Name, stageLog.CurrentStage, stageLog.CurrentStageDetail ?? string.Empty,
                stageLog.ElapsedMilliseconds, (int) budget!.Value.TotalSeconds, expensivePass, abandoned,
                consecutiveCuts);
            if (abandoned && updateTask != null)
            {
                ObserveAbandonedUpdate(updateTask, kind, parent, stageLog);
                await DrainAbandonedUpdate(updateTask, plan.AbandonDrain, kind, parent);
            }

            return null;
        }
    }

    /// <summary>
    /// An on-demand index runs inside the HTTP request's DI scope, which is disposed once the response is sent. Give an
    /// abandoned update a short, bounded time to stop (every Cosmos write re-checks the cancelled token) so it finishes
    /// while its scoped repositories, HTTP clients and the pass API cache are still alive. If it is still running after
    /// that, it may fail after the scope ends; that is logged, and the timed-out answer does not claim what was saved.
    /// </summary>
    private async Task DrainAbandonedUpdate(Task updateTask, TimeSpan drain, string kind, Publisher parent)
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
                " kind='{Kind}' parent-id='{ParentId}' parent-name='{ParentName}' drain-seconds='{DrainSeconds}'. The abandoned update is still running after the on-demand drain and may fail once the request scope is disposed.",
                kind, parent.Id, parent.Name, (int) drain.TotalSeconds);
        }
    }

    private BudgetPlan HourlyPlan(bool expensivePass) =>
        new(_budget.BudgetFor(expensivePass), _budget.AbandonGrace, TimeSpan.Zero);

    /// <summary>A parent's time budget (null: none), the grace before abandoning, and the drain after abandoning.</summary>
    private sealed record BudgetPlan(TimeSpan? Budget, TimeSpan AbandonGrace, TimeSpan AbandonDrain);

    /// <summary>
    /// An abandoned update keeps running in the background. Observe it so a late outcome is logged instead of becoming an
    /// unobserved task exception. Its cancelled token stops it before any further Cosmos write.
    /// </summary>
    private void ObserveAbandonedUpdate(Task updateTask, string kind, Publisher parent, PodcastIndexingStageLog stageLog)
    {
        updateTask.ContinueWith(task =>
            {
                var error = task.Exception?.GetBaseException();
                logger.LogWarning(
                    AbandonedCompletedMessagePrefix +
                    " kind='{Kind}' parent-id='{ParentId}' parent-name='{ParentName}' status='{Status}' last-stage='{Stage}' elapsed-ms='{ElapsedMs}' error-type='{ErrorType}'",
                    kind, parent.Id, parent.Name, task.Status, stageLog.CurrentStage, stageLog.ElapsedMilliseconds,
                    error?.GetType().Name ?? string.Empty);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// CatalogueMigrate keeps the podcast id for the parent and deletes the podcast last. If a move stopped after the
    /// parent was written (search quota, verify, or the source changed mid-move), the podcast and the parent both exist
    /// and both are auto-indexed. Indexing the parent too would save each new video twice (an Episode on the podcast
    /// and a NewsReport / TvShowEpisode on the parent), and a re-run of the move would then hold both. The podcast stays
    /// the indexed source until the move finishes or is rolled back, so the parent is skipped (not a failure).
    /// A same-id podcast the podcast pass would not index (removed, or not auto-indexed) does not defer the parent:
    /// skipping then would leave nothing indexed.
    /// </summary>
    private async Task<bool> SourcePodcastStillExists(string kind, Publisher parent)
    {
        if (await podcastRepository.GetPodcast(parent.Id) is not { } podcast || !IsIndexedByPodcastPass(podcast))
        {
            return false;
        }

        logger.LogWarning(
            SourcePodcastExistsMessagePrefix +
            " kind='{Kind}' parent-id='{ParentId}' parent-name='{ParentName}'. The podcast with the same id still exists (CatalogueMigrate move not finished); the podcast is indexed instead. Re-run the move or roll it back.",
            kind, parent.Id, parent.Name);
        return true;
    }

    /// <summary>
    /// Whether the hourly podcast pass would index <paramref name="podcast"/>: selected by <c>IndexIdProvider</c>
    /// (<c>Removed</c> undefined, null or false, and <c>IndexAllEpisodes</c> or a title regex) and then indexed by
    /// <c>PodcastsUpdater</c> (not removed, and <c>IndexAllEpisodes</c> or a non-blank regex). A whitespace-only regex is
    /// blank, as in <c>PodcastsUpdater</c>, <c>CatalogueMigratePlanner.IsAutoIndexed</c> and
    /// <c>CatalogueIndexingSource.IsAutoIndexed</c>.
    /// <para>
    /// <c>Removed == null</c> counts as not removed. In C# a podcast without a <c>removed</c> property and one with a
    /// stored <c>"removed": null</c> both read as null, so they cannot be told apart here; the hourly query therefore
    /// selects both (<c>NOT IS_DEFINED OR = null OR = false</c>, #1102), so a podcast this check defers to is always one
    /// the podcast pass indexes. Treating null as removed would stop deferring for every half-moved podcast and bring
    /// back the double indexing this check prevents.
    /// </para>
    /// </summary>
    private static bool IsIndexedByPodcastPass(Podcast podcast) =>
        podcast.Removed != true &&
        (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex));

    private static bool IsIndexable(Publisher parent) =>
        !parent.IsRemoved() && IndexingOf(parent) is { IsAutoIndexed: true };

    private static CatalogueIndexingSource? IndexingOf(Publisher parent) => parent switch
    {
        TvShow show => show.Indexing,
        NewsOrganisation organisation => organisation.Indexing,
        _ => null
    };

    private async Task<IReadOnlyList<Playable>> IndexTvShow(TvShow show, IndexingContext context)
    {
        var outcome = await IndexParent(
            show,
            show.Indexing!,
            (since, cancellationToken) => tvShowEpisodeRepository
                .GetByTvShowId(show.Id, PlayableCosmosFilters.ReleasedOnOrAfter<TvShowEpisode>(since))
                .ToListAsync(cancellationToken).AsTask(),
            (since, cancellationToken) => tvShowEpisodeRepository
                .GetByTvShowId(show.Id, PlayableCosmosFilters.HoldsSpotifyOrAppleLinkReleasedOnOrAfter<TvShowEpisode>(since))
                .ToListAsync(cancellationToken).AsTask(),
            episode => CatalogueParentKindMapper.ToTvShowEpisode(episode, show),
            context);
        var cancellationToken = context.CancellationToken;
        if (outcome.ChangedPlayables.Count > 0)
        {
            context.ReportStage("save-playables", $"changed={outcome.ChangedPlayables.Count}");
            cancellationToken.ThrowIfCancellationRequested();
            await tvShowEpisodeRepository.Save(outcome.ChangedPlayables);
            await PromoteHeroes(show, outcome.HeroIds, cancellationToken);
        }

        if (!outcome.ParentPatch.IsEmpty)
        {
            context.ReportStage("patch-parent");
            cancellationToken.ThrowIfCancellationRequested();
            if (!await tvShowRepository.PatchIndexerState(show.Id, outcome.ParentPatch))
            {
                LogParentGone("TvShow", show);
            }
        }

        LogOutcome("TvShow", show, outcome);
        return outcome.ChangedPlayables;
    }

    private async Task<IReadOnlyList<Playable>> IndexNewsOrganisation(NewsOrganisation organisation, IndexingContext context)
    {
        var outcome = await IndexParent(
            organisation,
            organisation.Indexing!,
            (since, cancellationToken) => newsReportRepository
                .GetByNewsOrganisationId(organisation.Id, PlayableCosmosFilters.ReleasedOnOrAfter<NewsReport>(since))
                .ToListAsync(cancellationToken).AsTask(),
            (since, cancellationToken) => newsReportRepository
                .GetByNewsOrganisationId(
                    organisation.Id, PlayableCosmosFilters.HoldsSpotifyOrAppleLinkReleasedOnOrAfter<NewsReport>(since))
                .ToListAsync(cancellationToken).AsTask(),
            episode => CatalogueParentKindMapper.ToNewsReport(episode, organisation),
            context);
        var cancellationToken = context.CancellationToken;
        if (outcome.ChangedPlayables.Count > 0)
        {
            context.ReportStage("save-playables", $"changed={outcome.ChangedPlayables.Count}");
            cancellationToken.ThrowIfCancellationRequested();
            await newsReportRepository.Save(outcome.ChangedPlayables);
            await PromoteHeroes(organisation, outcome.HeroIds, cancellationToken);
        }

        if (!outcome.ParentPatch.IsEmpty)
        {
            context.ReportStage("patch-parent");
            cancellationToken.ThrowIfCancellationRequested();
            if (!await newsOrganisationRepository.PatchIndexerState(organisation.Id, outcome.ParentPatch))
            {
                LogParentGone("NewsOrganisation", organisation);
            }
        }

        LogOutcome("NewsOrganisation", organisation, outcome);
        return outcome.ChangedPlayables;
    }

    /// <summary>
    /// Appends the saved new playables of an <see cref="CatalogueIndexingSource.AlwaysPromoteAsHero"/> parent to the
    /// edge hero list. Best effort: the promoter logs and swallows its own failures, so indexing is not failed.
    /// </summary>
    private async Task PromoteHeroes(Publisher parent, IReadOnlyList<Guid> heroIds, CancellationToken cancellationToken)
    {
        if (heroIds.Count == 0)
        {
            return;
        }

        HeroAutoPromoteLogger.LogAttempt(logger, EpisodeCreationSource.Indexer, parent.Id, heroIds);
        cancellationToken.ThrowIfCancellationRequested();
        await heroEpisodePromoter.PromoteAsync(heroIds, cancellationToken);
    }

    /// <summary>Added episodes eligible for auto-hero (flag on, not ignored / removed / term-filtered, in the week window).</summary>
    private List<Guid> SelectHeroIds(Podcast podcast, IEnumerable<Episode> addedEpisodes, ISet<Guid> filteredIds)
    {
        var heroIds = new List<Guid>();
        if (podcast.AlwaysPromoteAsHero != true)
        {
            return heroIds;
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var added in addedEpisodes)
        {
            var skipReason = filteredIds.Contains(added.Id)
                ? HeroAutoPromoteSkipReason.Removed
                : HeroAutoPromoteSelector.GetSkipReason(podcast, added, utcNow);
            if (skipReason != HeroAutoPromoteSkipReason.None)
            {
                HeroAutoPromoteLogger.LogSkipped(
                    logger,
                    skipReason,
                    added.Id,
                    podcast.Id,
                    podcast.AlwaysPromoteAsHero,
                    release: added.ReleaseUtc,
                    cutoff: skipReason == HeroAutoPromoteSkipReason.OutsideWeekWindow
                        ? HeroAutoPromoteSelector.GetCutoff(utcNow)
                        : null,
                    episodeResult: "Created");
                continue;
            }

            heroIds.Add(added.Id);
        }

        return heroIds;
    }

    private void LogParentGone(string kind, Publisher parent)
    {
        logger.LogWarning(
            "Catalogue parent kind='{Kind}' id='{ParentId}' name='{ParentName}' was deleted during the index pass; indexer state not written.",
            kind, parent.Id, parent.Name);
    }

    private void LogOutcome<TPlayable>(string kind, Publisher parent, ParentIndexOutcome<TPlayable> outcome)
        where TPlayable : Playable
    {
        logger.LogInformation(
            "Catalogue parent indexed kind='{Kind}' id='{ParentId}' name='{ParentName}' added='{Added}' merged='{Merged}' enriched='{Enriched}' removed-by-terms='{Filtered}' succeeded='{Succeeded}'.",
            kind, parent.Id, parent.Name, outcome.Added, outcome.Merged, outcome.Enriched, outcome.Filtered,
            outcome.Succeeded);
    }

    private async Task<ParentIndexOutcome<TPlayable>> IndexParent<TPlayable>(
        Publisher parent,
        CatalogueIndexingSource source,
        Func<DateTime, CancellationToken, Task<List<TPlayable>>> loadPlayables,
        Func<DateTime, CancellationToken, Task<List<TPlayable>>> loadAudioLinkHolders,
        Func<Episode, TPlayable> createPlayable,
        IndexingContext indexingContext)
        where TPlayable : Playable
    {
        var passStarted = timeProvider.GetTimestamp();
        // CatalogueIndexingSource reads a legacy blank id/regex as null, so blank-to-null alone never counts as a
        // change the pass writes back (it would overwrite a curator edit saved mid-pass).
        var indexingBefore = CatalogueIndexingChanges.Snapshot(source);
        var youTubeIndexingHintsBefore = source.YouTubeIndexingHints?.Copy();
        var cancellationToken = indexingContext.CancellationToken;
        var lastIndexedBefore = parent.LastIndexed;
        var latestReleasedBefore = parent.LatestReleased;
        var podcast = source.ToTransientPodcast(parent);
        var youTubePublishingDelay = podcast.YouTubePublishingDelay();
        var releasedSince = indexingContext.ReleasedSince!.Value;
        if (podcast.ReleaseAuthority == Service.YouTube)
        {
            releasedSince += youTubePublishingDelay;
        }

        var repositoryReleasedSince = youTubePublishingDelay < TimeSpan.Zero
            ? releasedSince
            : releasedSince - youTubePublishingDelay;

        indexingContext.ReportStage("cosmos-read-playables");
        var stored = await loadPlayables(repositoryReleasedSince, cancellationToken);
        var storedById = stored.ToDictionary(x => x.Id);
        var existing = stored.Select(x => ToEpisode(x, podcast)).ToList();

        var initialSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
        var initialSkipYouTube = indexingContext.SkipYouTubeUrlResolving;
        var expensivePass = !indexingContext.SkipExpensiveYouTubeQueries || !indexingContext.SkipExpensiveSpotifyQueries;
        indexingContext.ReportStage("discover-episodes", $"stored-in-window={stored.Count}");
        var newEpisodes = await episodeProvider.GetEpisodes(podcast, existing, indexingContext);
        cancellationToken.ThrowIfCancellationRequested();
        ApplyMinimumDuration(podcast, newEpisodes, indexingContext);

        indexingContext.ReportStage("merge", $"discovered={newEpisodes.Count}");
        var merge = episodeMerger.MergeEpisodes(podcast, existing, newEpisodes);
        indexingContext.ReportStage("sibling-podcast-check", $"added={merge.AddedEpisodes.Count}");
        var addedEpisodes = await ExcludeSiblingPodcastVideos(
            parent, podcast, merge.AddedEpisodes, repositoryReleasedSince, cancellationToken);
        if (podcast.HasIgnoreAllEpisodes())
        {
            foreach (var added in addedEpisodes)
            {
                added.Ignored = true;
            }
        }

        // The bypass check reads the skip flags as discovery left them: a Spotify rate limit hit by the optional
        // catch-up below must not stop lastIndexed moving for a pass whose discovery completed.
        var discoverySkipSpotify = indexingContext.SkipSpotifyUrlResolving;
        var discoverySkipYouTube = indexingContext.SkipYouTubeUrlResolving;
        var inScope = existing.Concat(addedEpisodes).ToList();
        indexingContext.ReportStage("enrich-audio-services", $"in-window={inScope.Count}");
        // The provider already used this clone with the flow the batch passed in. Catch-up is the Index flow
        // (newest pages, no title search), the same entry point as PodcastUpdater, on the clone IndexWithinBudget
        // made so this pass's provider lookup is unchanged.
        EpisodeLookupUseCases.ForIndex(indexingContext);
        var enrichedIds = await EnrichMissingAudioServices(
            parent,
            podcast,
            inScope,
            (since, ct) => LoadAudioLinkHolders(loadAudioLinkHolders, since, podcast, ct),
            (since, ct) => LoadAudioLinkHolders(loadPlayables, since, podcast, ct),
            repositoryReleasedSince,
            CatchUpDeadline(passStarted, expensivePass),
            indexingContext);
        cancellationToken.ThrowIfCancellationRequested();

        indexingContext.ReportStage("filter", $"added={addedEpisodes.Count} enriched={enrichedIds.Count}");
        var eliminationTerms = (await eliminationTermsProviderInstance.GetAsync()).GetEliminationTerms();
        var filtered = podcastFilter.Filter(podcast, inScope, eliminationTerms.Terms);

        var changed = new Dictionary<Guid, TPlayable>();
        foreach (var (merged, _) in merge.MergedEpisodes)
        {
            if (storedById.TryGetValue(merged.Id, out var target))
            {
                ApplyMerged(merged, target);
                changed[target.Id] = target;
            }
        }

        var existingById = existing.ToDictionary(x => x.Id);
        foreach (var enrichedId in enrichedIds)
        {
            if (storedById.TryGetValue(enrichedId, out var target) && existingById.TryGetValue(enrichedId, out var enriched))
            {
                ApplyEnrichedServices(enriched, target);
                changed[target.Id] = target;
            }
        }

        foreach (var filteredEpisode in filtered.FilteredEpisodes)
        {
            if (storedById.TryGetValue(filteredEpisode.Episode.Id, out var target))
            {
                target.Removed = true;
                changed[target.Id] = target;
            }
        }

        foreach (var added in addedEpisodes)
        {
            changed[added.Id] = createPlayable(added);
        }

        source.CaptureFrom(podcast);
        if (merge.FailedEpisodes.Any() && !Equals(youTubeIndexingHintsBefore, source.YouTubeIndexingHints))
        {
            // Same rule as PodcastUpdater: a failed merge keeps the previous hints, so the next pass offers the
            // failed episode again instead of the patch moving the watermark and count past it.
            logger.LogInformation(
                "YouTubeIndexingHints kept previous hints: parent-id='{ParentId}' failed-episodes='{FailedEpisodes}'.",
                parent.Id, merge.FailedEpisodes.Count());
            source.YouTubeIndexingHints = youTubeIndexingHintsBefore;
        }

        var bypassed = initialSkipSpotify != discoverySkipSpotify ||
                       initialSkipYouTube != discoverySkipYouTube ||
                       podcast.IsScheduledYouTubeDiscoveryBypassed(indexingContext);
        var succeeded = !bypassed && !merge.FailedEpisodes.Any();
        if (succeeded)
        {
            parent.LastIndexed = timeProvider.GetUtcNow().UtcDateTime;
        }

        var newest = addedEpisodes.Select(x => (DateTime?)x.ReleaseUtc)
            .Concat(merge.MergedEpisodes.Select(x => (DateTime?)x.Existing.ReleaseUtc))
            .Max();
        if (newest.HasValue && (parent.LatestReleased == null || newest > parent.LatestReleased))
        {
            parent.LatestReleased = newest;
        }

        // Write back only what this pass moved (and only the Indexer-owned fields), so a curator edit saved
        // mid-pass is not overwritten by this pass's snapshot of the parent.
        var parentPatch = new CatalogueParentIndexerPatch(
            parent.LastIndexed != lastIndexedBefore ? parent.LastIndexed : null,
            parent.LatestReleased != latestReleasedBefore ? parent.LatestReleased : null,
            source,
            CatalogueIndexingChanges.ChangedSince(indexingBefore, source));
        return new ParentIndexOutcome<TPlayable>(
            [.. changed.Values],
            parentPatch,
            addedEpisodes.Count,
            merge.MergedEpisodes.Count,
            enrichedIds.Count,
            filtered.FilteredEpisodes.Count,
            succeeded,
            SelectHeroIds(
                podcast,
                addedEpisodes,
                filtered.FilteredEpisodes.Select(x => x.Episode.Id).ToHashSet()));
    }

    /// <summary>Most catch-up candidates looked up in one parent pass (newest first).</summary>
    public const int AudioCatchUpMaxCandidatesPerPass = 5;

    /// <summary>Share of the parent's pass budget after which the catch-up stops looking up further candidates.</summary>
    public const double AudioCatchUpBudgetShare = 0.5;

    /// <summary>Stable log prefix for the per-parent Spotify / Apple catch-up summary line.</summary>
    public const string AudioCatchUpMessagePrefix = "IndexerCatalogueAudioCatchUp";

    /// <summary>Only items released this recently are looked up (older ones had their chances on earlier passes).</summary>
    public static readonly TimeSpan AudioCatchUpRecency = TimeSpan.FromDays(7);

    /// <summary>
    /// Items with a known length below this are not looked up: a short clip of a report is not the podcast episode, and
    /// skipping it keeps it from claiming the full episode's link. An unknown (zero) length is still looked up.
    /// </summary>
    public static readonly TimeSpan AudioCatchUpMinimumLength = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How far before the oldest candidate the link holders are read. The enrichers only match a Spotify / Apple
    /// episode released within days of the item, so a holder released earlier than this cannot hold a link a candidate
    /// could be given, and reading the parent's whole history every pass would cost RUs for nothing.
    /// </summary>
    public static readonly TimeSpan AudioCatchUpHolderLookback = TimeSpan.FromDays(30);

    private long? CatchUpDeadline(long passStarted, bool expensivePass)
    {
        if (_budget.BudgetFor(expensivePass) is not { } budget)
        {
            return null;
        }

        var share = budget * AudioCatchUpBudgetShare;
        return passStarted + (long)(share.TotalSeconds * timeProvider.TimestampFrequency);
    }

    private static async Task<List<Episode>> LoadAudioLinkHolders<TPlayable>(
        Func<DateTime, CancellationToken, Task<List<TPlayable>>> loadAudioLinkHolders,
        DateTime since,
        Podcast podcast,
        CancellationToken cancellationToken)
        where TPlayable : Playable
    {
        var holders = await loadAudioLinkHolders(since, cancellationToken);
        return holders.Select(x => ToEpisode(x, podcast)).ToList();
    }

    /// <summary>
    /// Offers recent in-window playables that lack a Spotify or Apple link to the podcast enrichers, within the limits
    /// in the class doc. Removed items are skipped, and a service is only looked up when the parent has that show /
    /// podcast id, so a parent without one costs no API call. Each enricher works on a copy of the item; only the
    /// copy's services and ids are copied back. Returns the ids of the items that gained a Spotify or Apple link.
    /// A lookup that throws (other than the pass being cancelled) is logged and the catch-up stops; the pass goes on.
    /// </summary>
    private async Task<HashSet<Guid>> EnrichMissingAudioServices(
        Publisher parent,
        Podcast podcast,
        IReadOnlyList<Episode> inWindow,
        Func<DateTime, CancellationToken, Task<List<Episode>>> loadHolders,
        Func<DateTime, CancellationToken, Task<List<Episode>>> loadReleasedOnOrAfter,
        DateTime repositoryReleasedSince,
        long? softDeadline,
        IndexingContext indexingContext)
    {
        var enriched = new HashSet<Guid>();
        var lookUpSpotify = !string.IsNullOrWhiteSpace(podcast.SpotifyId);
        var lookUpApple = podcast.AppleId is > 0;
        if (!lookUpSpotify && !lookUpApple)
        {
            return enriched;
        }

        var cancellationToken = indexingContext.CancellationToken;
        var recentSince = timeProvider.GetUtcNow().UtcDateTime - AudioCatchUpRecency;
        var missing = inWindow
            .Where(x => !x.Removed &&
                        ((lookUpSpotify && MissingSpotify(x)) || (lookUpApple && MissingApple(x))))
            .ToList();
        var recent = missing.Where(x => x.ReleaseUtc >= recentSince).ToList();
        var skippedOld = missing.Count - recent.Count;
        var longEnough = recent
            .Where(x => x.Length <= TimeSpan.Zero || x.Length >= AudioCatchUpMinimumLength)
            .ToList();
        var skippedShort = recent.Count - longEnough.Count;
        // Newest first picks which items this pass looks up. A shorter item stays out of a service while a
        // strictly longer same-title item still lacks that service and is not in this capped set. That longer
        // item may sit outside the recency window, outside the newest few, or older than this pass's release
        // cutoff while still inside the matcher's 14-day acceptance (#1161 leaves that tolerance unchanged).
        // When both are in the set, longest first lets the full item claim the link before the shorter one.
        var capped = longEnough
            .OrderByDescending(x => x.ReleaseUtc)
            .Take(AudioCatchUpMaxCandidatesPerPass)
            .ToList();
        var skippedByCap = longEnough.Count - capped.Count;
        var cappedIds = capped.Select(x => x.Id).ToHashSet();
        var stillMissing = new List<Episode>(missing);
        List<Episode> candidates = [];
        var skippedShorter = 0;
        var holderCount = 0;
        var lookedUp = 0;
        var skippedByBudget = 0;
        string? failure = null;
        if (longEnough.Count > 0)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Hourly passes cut the provider read at about two days, which is newer than the matcher's
                // acceptance. One release-filtered read brings in older gaps; they are not lookup candidates.
                var acceptanceSince = timeProvider.GetUtcNow().UtcDateTime
                    - EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold;
                if (repositoryReleasedSince > acceptanceSince)
                {
                    var knownIds = inWindow.Select(x => x.Id).ToHashSet();
                    var older = await loadReleasedOnOrAfter(acceptanceSince, cancellationToken);
                    stillMissing.AddRange(older.Where(episode =>
                        !knownIds.Contains(episode.Id) &&
                        !episode.Removed &&
                        ((lookUpSpotify && MissingSpotify(episode)) || (lookUpApple && MissingApple(episode)))));
                }

                bool OfferSpotify(Episode episode) =>
                    lookUpSpotify &&
                    MissingSpotify(episode) &&
                    !DeferredBehindLongerSameTitle(episode, stillMissing, cappedIds, MissingSpotify);
                bool OfferApple(Episode episode) =>
                    lookUpApple &&
                    MissingApple(episode) &&
                    !DeferredBehindLongerSameTitle(episode, stillMissing, cappedIds, MissingApple);
                candidates = capped
                    .Where(episode => OfferSpotify(episode) || OfferApple(episode))
                    .OrderByDescending(x => x.Length)
                    .ThenBy(x => x.ReleaseUtc)
                    .ToList();
                skippedShorter = capped.Count - candidates.Count;
                if (candidates.Count > 0)
                {
                    var holdersSince = candidates.Min(x => x.ReleaseUtc) - AudioCatchUpHolderLookback;
                    var inWindowIds = inWindow.Select(x => x.Id).ToHashSet();
                    var holders = (await loadHolders(holdersSince, cancellationToken))
                        .Where(x => !inWindowIds.Contains(x.Id))
                        .ToList();
                    holderCount = holders.Count;
                    // In-window instances come first and win: they carry this pass's merges and earlier catch-up links.
                    IReadOnlyList<Episode> requestEpisodes = [.. inWindow, .. holders]; // pragma: allowlist secret
                    foreach (var episode in candidates)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (softDeadline is { } deadline && timeProvider.GetTimestamp() >= deadline)
                        {
                            skippedByBudget = candidates.Count - lookedUp;
                            break;
                        }

                        lookedUp++;
                        if (await EnrichCandidate(
                                podcast,
                                requestEpisodes, // pragma: allowlist secret
                                episode,
                                OfferSpotify(episode),
                                OfferApple(episode),
                                indexingContext))
                        {
                            enriched.Add(episode.Id);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                failure = ex.GetType().Name;
                logger.LogWarning(ex,
                    "{Prefix} failed parent-id='{ParentId}' name='{ParentName}'; the pass continues without it.",
                    AudioCatchUpMessagePrefix, parent.Id, parent.Name);
            }
        }

        logger.LogInformation(
            "{Prefix} parent-id='{ParentId}' name='{ParentName}' missing='{Missing}' candidates='{Candidates}' looked-up='{LookedUp}' enriched='{Enriched}' holders='{Holders}' skipped-old='{SkippedOld}' skipped-short='{SkippedShort}' skipped-shorter='{SkippedShorter}' skipped-by-cap='{SkippedByCap}' skipped-by-budget='{SkippedByBudget}' failure='{Failure}'.",
            AudioCatchUpMessagePrefix, parent.Id, parent.Name, missing.Count, candidates.Count, lookedUp, enriched.Count,
            holderCount, skippedOld, skippedShort, skippedShorter, skippedByCap, skippedByBudget, failure);
        return enriched;
    }

    /// <summary>
    /// Runs the Spotify then Apple enricher on a copy of <paramref name="episode"/> and copies back only the services
    /// and ids, so a release or description refinement by the enrichers never reaches the item (or a merge applied to
    /// it later in the pass). Spotify is not called once the pass has hit a Spotify rate limit.
    /// </summary>
    private async Task<bool> EnrichCandidate(
        Podcast podcast,
        IReadOnlyList<Episode> requestEpisodes,
        Episode episode,
        bool lookUpSpotify,
        bool lookUpApple,
        IndexingContext indexingContext)
    {
        var probe = CopyForEnrichment(episode);
        var enrichmentContext = new EnrichmentContext();
        var request = new EnrichmentRequest(podcast, requestEpisodes, probe);
        if (lookUpSpotify && MissingSpotify(probe) && !indexingContext.SkipSpotifyUrlResolving)
        {
            await spotifyEpisodeEnricher.Enrich(request, indexingContext, enrichmentContext);
        }

        if (lookUpApple && MissingApple(probe))
        {
            indexingContext.CancellationToken.ThrowIfCancellationRequested();
            await appleEpisodeEnricher.Enrich(request, indexingContext, enrichmentContext);
        }

        // Only a new service link is written back; a release-only refinement is not worth a playable write.
        if (!enrichmentContext.SpotifyUrlUpdated && !enrichmentContext.AppleUrlUpdated)
        {
            return false;
        }

        episode.Services = CopyServices(probe.Services);
        episode.Ids = probe.Ids is null ? null : CopyIds(probe.Ids);
        return true;
    }

    /// <summary>The fields the enrichers read, on a separate instance so their writes stay off the in-pass item.</summary>
    internal static Episode CopyForEnrichment(Episode episode)
    {
        var copy = new Episode
        {
            Id = episode.Id,
            PodcastId = episode.PodcastId,
            PodcastName = episode.PodcastName,
            Title = episode.Title,
            Description = episode.Description,
            Length = episode.Length,
            Explicit = episode.Explicit,
            Removed = episode.Removed,
            Ignored = episode.Ignored,
            Posted = episode.Posted,
            Tweeted = episode.Tweeted,
            Language = episode.Language,
            Subjects = [.. episode.Subjects],
            RemovedSubjects = [.. episode.RemovedSubjects],
            SearchTerms = episode.SearchTerms,
            HashTag = episode.HashTag,
            Services = CopyServices(episode.Services),
            Ids = episode.Ids is null ? null : CopyIds(episode.Ids)
        };
        copy.SetRelease(episode.Release);
        return copy;
    }

    private static Dictionary<string, ServiceLink>? CopyServices(Dictionary<string, ServiceLink>? services) =>
        services?.ToDictionary(
            pair => pair.Key,
            pair => new ServiceLink { Url = pair.Value.Url, Image = pair.Value.Image, Language = pair.Value.Language });

    private static EpisodeIds CopyIds(EpisodeIds ids) =>
        new() { Spotify = ids.Spotify, Apple = ids.Apple, YouTube = ids.YouTube };

    /// <summary>
    /// True when <paramref name="episode"/> lacks <paramref name="lacksService"/> and a strictly longer same-title
    /// item in <paramref name="stillMissing"/> also lacks it and is not in this pass's capped set. That list is the
    /// in-window gaps plus playables older than the release cutoff and still inside the matcher's acceptance window.
    /// The shorter item must not be offered
    /// that service: the matcher returns the first same-title row before it scores duration, so the shorter item
    /// would take the link the longer item still needs.
    /// </summary>
    private static bool DeferredBehindLongerSameTitle(
        Episode episode,
        IReadOnlyList<Episode> stillMissing,
        HashSet<Guid> cappedIds,
        Func<Episode, bool> lacksService)
    {
        if (!lacksService(episode))
        {
            return false;
        }

        foreach (var longer in stillMissing)
        {
            if (longer.Id == episode.Id ||
                longer.Length <= episode.Length ||
                cappedIds.Contains(longer.Id) ||
                !lacksService(longer) ||
                !SameTitle(longer, episode))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>Same comparison the catalogue matcher uses for an exact title (decoded, trimmed, ordinal).</summary>
    private static bool SameTitle(Episode left, Episode right) =>
        string.Equals(NormalisedTitle(left.Title), NormalisedTitle(right.Title), StringComparison.Ordinal);

    private static string NormalisedTitle(string? title) =>
        System.Net.WebUtility.HtmlDecode(title ?? string.Empty).Trim();

    private static bool MissingSpotify(Episode episode) =>
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify) == null ||
        string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode));

    private static bool MissingApple(Episode episode) =>
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple) == null ||
        EpisodeServicePresence.AppleEpisodeId(episode) is null or 0;

    /// <summary>
    /// A parent indexing a whole YouTube channel can share it with podcasts scoped to one playlist of that
    /// channel (e.g. a news outlet and one of its shows). A video those podcasts already hold as an episode is
    /// theirs, so it is not also saved as a NewsReport / TvShowEpisode. Only stored episodes are checked: a
    /// video the podcast has not indexed yet can still be taken by the parent first (see PR notes).
    /// </summary>
    private async Task<List<Episode>> ExcludeSiblingPodcastVideos(
        Publisher parent,
        Podcast transient,
        IList<Episode> added,
        DateTime releasedSince,
        CancellationToken cancellationToken)
    {
        var channelId = transient.YouTubeChannelId;
        var addedYouTubeIds = added
            .Select(EpisodeServicePresence.YouTubeEpisodeId)
            .Where(id => id != null)
            .ToHashSet();
        if (string.IsNullOrWhiteSpace(channelId) || !string.IsNullOrWhiteSpace(transient.YouTubePlaylistId) ||
            addedYouTubeIds.Count == 0)
        {
            return [.. added];
        }

        var siblings = await podcastRepository
            .GetAllBy(x => x.YouTubeChannelId == channelId && x.YouTubePlaylistId != "")
            .ToListAsync(cancellationToken);
        if (siblings.Count == 0)
        {
            return [.. added];
        }

        var held = new HashSet<string>();
        foreach (var sibling in siblings)
        {
            await foreach (var episode in episodeRepository.GetByPodcastId(
                               sibling.Id, EpisodeCosmosFilters.ReleasedOnOrAfter(releasedSince))
                               .WithCancellation(cancellationToken))
            {
                if (EpisodeServicePresence.YouTubeEpisodeId(episode) is { } id && addedYouTubeIds.Contains(id))
                {
                    held.Add(id);
                }
            }
        }

        if (held.Count == 0)
        {
            return [.. added];
        }

        logger.LogInformation(
            "Catalogue parent '{ParentName}' ({ParentId}): skipped {Count} video(s) already held by playlist-scoped podcast(s) {PodcastIds} on channel '{ChannelId}'.",
            parent.Name, parent.Id, held.Count, string.Join(", ", siblings.Select(x => x.Id)), channelId);
        return added.Where(x => EpisodeServicePresence.YouTubeEpisodeId(x) is not { } id || !held.Contains(id)).ToList();
    }

    private void ApplyMinimumDuration(Podcast podcast, IList<Episode> newEpisodes, IndexingContext indexingContext)
    {
        var bypassGlobalShortCheck = podcast.BypassShortEpisodeChecking == true;
        var enforceMinimumDuration = podcast.MinimumDuration.HasValue || !bypassGlobalShortCheck;
        if (!enforceMinimumDuration)
        {
            return;
        }

        var threshold = podcast.MinimumDuration ?? _postingCriteria.MinimumDuration;
        foreach (var newEpisode in newEpisodes)
        {
            newEpisode.Ignored = newEpisode.Length < threshold;
        }

        if (indexingContext.SkipShortEpisodes || podcast.MinimumDuration.HasValue)
        {
            foreach (var shortEpisode in newEpisodes.Where(x => x.Ignored).ToList())
            {
                newEpisodes.Remove(shortEpisode);
            }
        }
    }

    /// <summary>Episode view of a stored playable, for the podcast merger and filters.</summary>
    internal static Episode ToEpisode(Playable playable, Podcast podcast)
    {
        var episode = new Episode
        {
            Id = playable.Id,
            PodcastId = podcast.Id,
            PodcastName = podcast.Name,
            Title = playable.Title,
            Description = playable.Description,
            Length = playable.Length,
            Explicit = playable.Explicit,
            Posted = playable.Posted,
            Tweeted = playable.Tweeted,
            Ignored = playable.Ignored,
            Removed = playable.Removed,
            Language = playable.Language,
            Subjects = [.. playable.Subjects],
            RemovedSubjects = [.. playable.RemovedSubjects],
            SearchTerms = playable.SearchTerms,
            HashTag = playable.HashTag,
            Services = playable.Services?.ToDictionary(
                pair => pair.Key,
                pair => new ServiceLink { Url = pair.Value.Url, Image = pair.Value.Image, Language = pair.Value.Language })
        };
        episode.SetRelease(playable.Release);
        episode.Ids = IdsFromServices(playable.Services);
        return episode;
    }

    /// <summary>Spotify / Apple / YouTube ids parsed from <c>services</c> URLs (playables store no provider ids).</summary>
    internal static EpisodeIds? IdsFromServices(IReadOnlyDictionary<string, ServiceLink>? services)
    {
        if (services is null || services.Count == 0)
        {
            return null;
        }

        var ids = new EpisodeIds();
        if (services.TryGetValue(ServiceKeys.Spotify, out var spotify) && spotify.Url is not null)
        {
            var spotifyId = SpotifyIdResolver.GetEpisodeId(spotify.Url);
            ids.Spotify = string.IsNullOrWhiteSpace(spotifyId) ? null : spotifyId;
        }

        if (services.TryGetValue(ServiceKeys.Apple, out var apple) && apple.Url is not null)
        {
            ids.Apple = AppleIdResolver.GetEpisodeId(apple.Url);
        }

        if (services.TryGetValue(ServiceKeys.YouTube, out var youTube) && youTube.Url is not null)
        {
            ids.YouTube = YouTubeIdResolver.Extract(youTube.Url);
        }

        return ids.IsEmpty ? null : ids;
    }

    private static void ApplyMerged(Episode merged, Playable target)
    {
        target.Title = merged.Title;
        target.Description = merged.Description;
        target.Length = merged.Length;
        target.Explicit = merged.Explicit;
        target.SetRelease(merged.Release);
        target.Services = merged.Services?.ToDictionary(
            pair => pair.Key,
            pair => new ServiceLink { Url = pair.Value.Url, Image = pair.Value.Image, Language = pair.Value.Language });
        if (target is ICatalogueYouTubeItem catalogueYouTube)
        {
            // Keep the compact id the search pull reads (c.youTubeId) in step with services.youtube.url.
            // Url wins; a missing or unparseable url never clears an existing id.
            EpisodeServicePresence.SyncCatalogueYouTubeIdFromUrl(catalogueYouTube);
        }
    }

    /// <summary>
    /// Copies only the service links an enricher added (curator title, description, release and length stay as
    /// stored), keeping <c>youTubeId</c> in step as <see cref="ApplyMerged"/> does.
    /// </summary>
    private static void ApplyEnrichedServices(Episode enriched, Playable target)
    {
        target.Services = enriched.Services?.ToDictionary(
            pair => pair.Key,
            pair => new ServiceLink { Url = pair.Value.Url, Image = pair.Value.Image, Language = pair.Value.Language });
        if (target is ICatalogueYouTubeItem catalogueYouTube)
        {
            EpisodeServicePresence.SyncCatalogueYouTubeIdFromUrl(catalogueYouTube);
        }
    }

    private sealed record ParentIndexOutcome<TPlayable>(
        IReadOnlyList<TPlayable> ChangedPlayables,
        CatalogueParentIndexerPatch ParentPatch,
        int Added,
        int Merged,
        int Enriched,
        int Filtered,
        bool Succeeded,
        IReadOnlyList<Guid> HeroIds)
        where TPlayable : Playable;
}
