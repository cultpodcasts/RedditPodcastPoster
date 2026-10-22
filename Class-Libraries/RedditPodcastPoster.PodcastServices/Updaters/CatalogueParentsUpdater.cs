using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.DependencyInjection;
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
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
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
/// merged and term-removed items update the stored playable. Cross-platform enrichment and
/// hero promotion are not run for these kinds.
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
                            context => IndexTvShow(tvShow, context), indexingContext, expensivePass);
                    }

                    continue;
                }

                var organisation = await newsOrganisationRepository.GetNewsOrganisation(parentId);
                if (organisation is not null && IsIndexable(organisation) &&
                    !await SourcePodcastStillExists("NewsOrganisation", organisation))
                {
                    success &= await IndexWithinBudget("NewsOrganisation", organisation,
                        context => IndexNewsOrganisation(organisation, context), indexingContext, expensivePass);
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

    /// <summary>
    /// Runs one parent's index under its time budget. Returns false when the budget cut it off; other failures
    /// propagate to the caller's per-parent catch.
    /// </summary>
    private async Task<bool> IndexWithinBudget(
        string kind,
        Publisher parent,
        Func<IndexingContext, Task> index,
        IndexingContext indexingContext,
        bool expensivePass)
    {
        var stageLog = new PodcastIndexingStageLog(parent.Id, parent.Name, logger, timeProvider, _stageLogOptions);
        var budget = _budget.BudgetFor(expensivePass);
        Task? updateTask = null;
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
            if (budget.HasValue)
            {
                await updateTask.WaitAsync(budget.Value + _budget.AbandonGrace, timeProvider);
            }
            else
            {
                await updateTask;
            }

            stageLog.Stage("complete");
            budgetCutTracker.RecordCompleted(parent.Id);
            return true;
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
            }

            return false;
        }
    }

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
    /// (<c>Removed</c> undefined or false, and <c>IndexAllEpisodes</c> or a title regex) and then indexed by
    /// <c>PodcastsUpdater</c> (not removed, and <c>IndexAllEpisodes</c> or a non-blank regex). A whitespace-only regex is
    /// blank, as in <c>PodcastsUpdater</c>, <c>CatalogueMigratePlanner.IsAutoIndexed</c> and
    /// <c>CatalogueIndexingSource.IsAutoIndexed</c>.
    /// <para>
    /// <c>Removed == null</c> counts as not removed. In C# a podcast without a <c>removed</c> property (the case the hourly
    /// query includes via <c>!Removed.IsDefined()</c>) and one with a stored <c>"removed": null</c> both read as null, so
    /// they cannot be told apart here. Null is never written (<c>JsonSerializerOptionsProvider</c> uses
    /// <c>WhenWritingNull</c>), so null here means "property absent", which the hourly query indexes. Treating null as
    /// removed would stop deferring for every half-moved podcast and bring back the double indexing this check prevents.
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

    private async Task IndexTvShow(TvShow show, IndexingContext context)
    {
        var outcome = await IndexParent(
            show,
            show.Indexing!,
            (since, cancellationToken) => tvShowEpisodeRepository
                .GetByTvShowId(show.Id, PlayableCosmosFilters.ReleasedOnOrAfter<TvShowEpisode>(since))
                .ToListAsync(cancellationToken).AsTask(),
            episode => CatalogueParentKindMapper.ToTvShowEpisode(episode, show),
            context);
        var cancellationToken = context.CancellationToken;
        if (outcome.ChangedPlayables.Count > 0)
        {
            context.ReportStage("save-playables", $"changed={outcome.ChangedPlayables.Count}");
            cancellationToken.ThrowIfCancellationRequested();
            await tvShowEpisodeRepository.Save(outcome.ChangedPlayables);
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
    }

    private async Task IndexNewsOrganisation(NewsOrganisation organisation, IndexingContext context)
    {
        var outcome = await IndexParent(
            organisation,
            organisation.Indexing!,
            (since, cancellationToken) => newsReportRepository
                .GetByNewsOrganisationId(organisation.Id, PlayableCosmosFilters.ReleasedOnOrAfter<NewsReport>(since))
                .ToListAsync(cancellationToken).AsTask(),
            episode => CatalogueParentKindMapper.ToNewsReport(episode, organisation),
            context);
        var cancellationToken = context.CancellationToken;
        if (outcome.ChangedPlayables.Count > 0)
        {
            context.ReportStage("save-playables", $"changed={outcome.ChangedPlayables.Count}");
            cancellationToken.ThrowIfCancellationRequested();
            await newsReportRepository.Save(outcome.ChangedPlayables);
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
            "Catalogue parent indexed kind='{Kind}' id='{ParentId}' name='{ParentName}' added='{Added}' merged='{Merged}' removed-by-terms='{Filtered}' succeeded='{Succeeded}'.",
            kind, parent.Id, parent.Name, outcome.Added, outcome.Merged, outcome.Filtered, outcome.Succeeded);
    }

    private async Task<ParentIndexOutcome<TPlayable>> IndexParent<TPlayable>(
        Publisher parent,
        CatalogueIndexingSource source,
        Func<DateTime, CancellationToken, Task<List<TPlayable>>> loadPlayables,
        Func<Episode, TPlayable> createPlayable,
        IndexingContext indexingContext)
        where TPlayable : Playable
    {
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

        indexingContext.ReportStage("filter", $"added={addedEpisodes.Count}");
        var inScope = existing.Concat(addedEpisodes).ToList();
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

        var bypassed = initialSkipSpotify != indexingContext.SkipSpotifyUrlResolving ||
                       initialSkipYouTube != indexingContext.SkipYouTubeUrlResolving ||
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
            filtered.FilteredEpisodes.Count,
            succeeded);
    }

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
    }

    private sealed record ParentIndexOutcome<TPlayable>(
        IReadOnlyList<TPlayable> ChangedPlayables,
        CatalogueParentIndexerPatch ParentPatch,
        int Added,
        int Merged,
        int Filtered,
        bool Succeeded)
        where TPlayable : Playable;
}
