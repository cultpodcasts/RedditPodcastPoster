using System.Collections.Concurrent;
using System.Text.Json;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.News;

/// <summary>
/// Moves news reports from one news organisation to another, keeping their ids (#1397; first use: folding the two
/// "Associated Press" organisations into one). <see cref="NewsReport"/> is partitioned by <c>newsOrganisationId</c>, so a
/// move writes the report into the target's partition and then deletes the source row.
/// <para>
/// Every report keeps its content, services, subjects, posted / tweeted / Bluesky state and its ignored and removed
/// flags; only the parent fields change (<see cref="NewsReport.SetNewsOrganisationProperties"/>: id, name,
/// parent-removed, publisher search terms and language). Removed reports move too, as in Move to news, and their
/// search documents are deleted instead of rewritten.
/// </para>
/// The whole request holds the parent indexing lease on both organisations (<see cref="IParentIndexingLockStore"/>, the
/// same single-flight marker the indexers take), so an indexer pass cannot write a source row after it was read or index
/// the source's channel mid-move. If either organisation is being indexed the request is refused (409, nothing written).
/// Order, so a failure never loses a report:
/// <list type="number">
/// <item>Check every requested id before any write: an id that is not in the source (and not already in the target), or
/// that is a report of a third organisation, a TV episode or a film, or whose YouTube video the target already has as a
/// different report, refuses the whole request.</item>
/// <item>Save each report into the target.</item>
/// <item>Swap each search document under the same id (now <c>seriesName</c> = the target's name). On failure the source
/// rows are kept.</item>
/// <item>Re-read the target and each saved report; a concurrent delete keeps the source rows.</item>
/// <item>Delete the moved source rows (a row already gone counts as deleted). If some deletes fail, the result is
/// Failed and lists only the rows that really moved.</item>
/// <item>Patch the target's <c>latestReleased</c> (conditional, never lowered) and delete the short-link records of the
/// moved reports and of any already moved by an earlier request, so a re-send always finishes these steps. The
/// Shortner then resolves each id from search and redirects to the target's page. Both best effort.</item>
/// <item>With <c>moveYouTubeChannels</c>: move the source's main and additional YouTube channels into the target's
/// <c>additionalYouTubeChannels</c> (source cleared first, so no channel is ever claimed twice). If the target save
/// fails, the source's channels are restored, so a re-send retries. A main channel that indexes a playlist owned by
/// another channel is refused (the target would otherwise index the whole channel); set the target by hand with the
/// organisation PATCH. The main channel's playlist history, playlist order and indexing hints are not carried.</item>
/// </list>
/// Re-sending resumes: a report already in the target while its source row exists is rewritten from the source row;
/// with the source row gone, the id is reported as already moved. The source organisation itself is never removed or
/// deleted here; when it is left empty the result names it so it can be retired (<c>DELETE newsorganisation/{id}</c>).
/// </summary>
public class NewsReportOrganisationMoveService(
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    IFilmRepository filmRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IShortnerService shortnerService,
    IParentIndexingLockStore indexingLockStore,
    TimeProvider timeProvider,
    ILogger<NewsReportOrganisationMoveService> logger) : INewsReportOrganisationMoveService
{
    /// <summary>Ids per Cosmos <c>IN</c> lookup.</summary>
    private const int LookupChunkSize = 100;

    /// <summary>Lock-marker source, so a log of a refused index names the move.</summary>
    public const string LockSource = "NewsReportOrganisationMove";

    /// <summary>
    /// Renews the marker well inside the indexers' stale window (<see cref="CatalogueParentsUpdater.IndexingLockStaleAfter"/>,
    /// shared so a live marker is never taken over) while a batch runs.
    /// </summary>
    public static readonly TimeSpan LockHeartbeatInterval = TimeSpan.FromMinutes(1);

    public const string ChannelMoveRetryError =
        "The YouTube channel move failed and the source's channels were left in place. Re-send with moveYouTubeChannels to retry";

    public const string ChannelRestoreFailedError =
        "The YouTube channel move failed and the source's channels could not be restored. Set them with PATCH newsorganisation/{id}; the original channels are in the error log";

    public const string ForeignPlaylistError =
        "The source's main YouTube channel indexes a playlist owned by another channel, so no channel was moved. Set the target's channels with PATCH newsorganisation/{id}";

    public async Task<NewsReportOrganisationMoveResult> MoveAsync(
        NewsReportOrganisationMoveCommand command,
        CancellationToken cancellationToken)
    {
        if (Validate(command) is { } invalid)
        {
            return invalid;
        }

        NewsOrganisation? target = null;
        MovePlan? plan = null;
        try
        {
            var source = await newsOrganisationRepository.GetNewsOrganisation(command.SourceNewsOrganisationId);
            if (source is null)
            {
                return NotFound("Source news organisation not found");
            }

            target = await newsOrganisationRepository.GetNewsOrganisation(command.TargetNewsOrganisationId);
            if (target is null)
            {
                return NotFound("Target news organisation not found");
            }

            if (target.Removed == true)
            {
                return BadRequest("A removed news organisation cannot take reports");
            }

            await using var sourceLease = await TryLease(source.Id);
            if (sourceLease is null)
            {
                return Busy(source.Id, target);
            }

            await using var targetLease = await TryLease(target.Id);
            if (targetLease is null)
            {
                return Busy(source.Id, target);
            }

            var requestedIds = command.ReportIds is { } ids
                ? ids.Distinct().ToList()
                : await NextSourceReportIds(source.Id, cancellationToken);
            plan = await Plan(source.Id, target.Id, requestedIds, cancellationToken);
            if (plan.NotFound.Count > 0 || plan.Conflicts.Count > 0)
            {
                logger.LogWarning(
                    "{method}: Refused moving {requested} report(s) from news organisation '{sourceId}' to '{targetId}': {notFound} not in the source, {conflicts} conflicting ({duplicates} a video the target already has). Nothing written.",
                    nameof(MoveAsync), requestedIds.Count, source.Id, target.Id, plan.NotFound.Count,
                    plan.Conflicts.Count, plan.DuplicateVideos);
                return new NewsReportOrganisationMoveResult(
                    plan.NotFound.Count > 0
                        ? NewsReportOrganisationMoveStatus.NotFound
                        : NewsReportOrganisationMoveStatus.Conflict,
                    source.Id,
                    target.Id,
                    target.Name.Trim(),
                    MovedReportIds: [],
                    AlreadyMovedReportIds: plan.AlreadyMoved,
                    NotFoundReportIds: plan.NotFound,
                    ConflictReportIds: plan.Conflicts,
                    Error: plan.NotFound.Count > 0
                        ? "Some report ids are not reports of the source news organisation. Nothing was moved"
                        : "Some report ids are a report of another organisation, a TV episode, a film, or a YouTube video the target already has as another report. Nothing was moved");
            }

            List<NewsReport> movedReports = [];
            var deleteFailures = 0;
            if (plan.ToMove.Count > 0)
            {
                var reports = plan.ToMove;
                foreach (var report in reports)
                {
                    report.SetNewsOrganisationProperties(target);
                }

                await SaveReports(reports);
                if (!await SwapSearch(target, reports, cancellationToken))
                {
                    logger.LogError(
                        "{method}: Search swap failed moving {count} report(s) from news organisation '{sourceId}' to '{targetId}'. Source rows kept; re-send to resume.",
                        nameof(MoveAsync), reports.Count, source.Id, target.Id);
                    return Failed(source.Id, target, [], plan.AlreadyMoved, indexing: true);
                }

                // Last check before the only irreversible step: a concurrent delete of the target or of a copied report
                // would leave the report with nowhere to live once its source row is gone.
                if (await newsOrganisationRepository.GetNewsOrganisation(target.Id) is null)
                {
                    logger.LogError(
                        "{method}: Target news organisation '{targetId}' is gone (changed concurrently). Source rows kept; re-send to resume.",
                        nameof(MoveAsync), target.Id);
                    return Failed(source.Id, target, [], plan.AlreadyMoved, indexing: true);
                }

                foreach (var report in reports)
                {
                    if (await newsReportRepository.GetNewsReport(target.Id, report.Id) is null)
                    {
                        logger.LogError(
                            "{method}: News report '{reportId}' is gone from the target (changed concurrently). Source rows kept; re-send to resume.",
                            nameof(MoveAsync), report.Id);
                        return Failed(source.Id, target, [], plan.AlreadyMoved, indexing: true);
                    }
                }

                var deleted = await DeleteSourceRows(source.Id, reports);
                movedReports = reports.Where(report => deleted.Contains(report.Id)).ToList();
                deleteFailures = reports.Count - movedReports.Count;
                logger.LogInformation(
                    "Moved {count} news report(s) from news organisation '{sourceName}' ('{sourceId}') to '{targetName}' ('{targetId}'); {failed} source row delete(s) failed.",
                    movedReports.Count, source.Name, source.Id, target.Name, target.Id, deleteFailures);
            }
            else
            {
                logger.LogInformation(
                    "{method}: Nothing left to move from news organisation '{sourceId}' to '{targetId}' ({alreadyMoved} already moved).",
                    nameof(MoveAsync), source.Id, target.Id, plan.AlreadyMoved.Count);
            }

            // Also for reports an earlier request moved: if it failed part-way these steps never ran for them.
            List<NewsReport> settled = [.. movedReports, .. plan.AlreadyMovedReports];
            await RaiseLatestReleased(target, settled);
            foreach (var report in settled)
            {
                await NewsReportMoveSource.DeleteShortLink(shortnerService, logger, report);
            }

            var moved = movedReports.Select(report => report.Id).ToList();
            if (deleteFailures > 0)
            {
                logger.LogError(
                    "{method}: {failed} of {count} source row(s) of news organisation '{sourceId}' could not be deleted; their reports are in both organisations. Re-send to finish.",
                    nameof(MoveAsync), deleteFailures, plan.ToMove.Count, source.Id);
                return Failed(source.Id, target, moved, plan.AlreadyMoved, indexing: false,
                    $"{moved.Count} of {plan.ToMove.Count} reports moved; {deleteFailures} source row(s) could not be deleted and are still in the source. Re-send to finish")
                    with
                    {
                        RemainingReportCount = await CountRemaining(source.Id)
                    };
            }

            var channels = ChannelMoveOutcome.None;
            if (command.MoveYouTubeChannels)
            {
                channels = await MoveYouTubeChannels(source.Id, target.Id);
            }

            var remaining = await CountRemaining(source.Id);
            var emptied = remaining == 0 && source.Removed != true ? source.Id : (Guid?) null;
            return new NewsReportOrganisationMoveResult(
                NewsReportOrganisationMoveStatus.Accepted,
                source.Id,
                target.Id,
                target.Name.Trim(),
                moved,
                plan.AlreadyMoved,
                [],
                [],
                Error: channels.Error)
            {
                RemainingReportCount = remaining,
                EmptiedNewsOrganisationId = emptied,
                MovedYouTubeChannelIds = channels.Added,
                YouTubeChannelMoveFailed = channels.Failed
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed moving reports from news organisation '{sourceId}' to '{targetId}'. Source rows not yet moved are kept; re-send to resume.",
                nameof(MoveAsync), command.SourceNewsOrganisationId, command.TargetNewsOrganisationId);
            return target is null
                ? new NewsReportOrganisationMoveResult(NewsReportOrganisationMoveStatus.Failed,
                    Error: "Unable to move the reports. Reports not yet moved are still in the source; re-send to resume")
                : Failed(command.SourceNewsOrganisationId, target, [], plan?.AlreadyMoved ?? [], indexing: false);
        }
    }

    private sealed record MovePlan(
        IReadOnlyList<NewsReport> ToMove,
        IReadOnlyList<Guid> AlreadyMoved,
        IReadOnlyList<NewsReport> AlreadyMovedReports,
        IReadOnlyList<Guid> NotFound,
        IReadOnlyList<Guid> Conflicts,
        int DuplicateVideos);

    private sealed record ChannelMoveOutcome(IReadOnlyList<string> Added, bool Failed, string? Error)
    {
        public static readonly ChannelMoveOutcome None = new([], false, null);

        public static ChannelMoveOutcome Fail(string error) => new([], true, error);
    }

    private Task<ParentIndexingLease?> TryLease(Guid organisationId) =>
        ParentIndexingLease.TryStart(
            indexingLockStore,
            timeProvider,
            organisationId,
            CatalogueParentsUpdater.IndexingLockStaleAfter,
            LockHeartbeatInterval,
            LockSource,
            logger);

    private NewsReportOrganisationMoveResult Busy(Guid sourceId, NewsOrganisation target)
    {
        logger.LogWarning(
            "{method}: News organisation '{sourceId}' or '{targetId}' is being indexed; move refused, nothing written.",
            nameof(MoveAsync), sourceId, target.Id);
        return new NewsReportOrganisationMoveResult(NewsReportOrganisationMoveStatus.Conflict, sourceId, target.Id,
            target.Name.Trim(), [], [], [], [],
            Error: "One of the news organisations is being indexed. Nothing was moved; retry in a few minutes")
        {
            Retryable = true
        };
    }

    /// <summary>The source's next batch of report ids (removed reports included), when the request names none.</summary>
    private async Task<List<Guid>> NextSourceReportIds(Guid sourceId, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        await foreach (var report in newsReportRepository.GetByNewsOrganisationId(sourceId)
                           .WithCancellation(cancellationToken))
        {
            ids.Add(report.Id);
            if (ids.Count == NewsReportOrganisationMoveLimits.MaxReportsPerRequest)
            {
                break;
            }
        }

        return ids;
    }

    /// <summary>Classifies every requested id. Writes nothing.</summary>
    private async Task<MovePlan> Plan(
        Guid sourceId,
        Guid targetId,
        IReadOnlyList<Guid> reportIds,
        CancellationToken cancellationToken)
    {
        var reportsById = new Dictionary<Guid, List<NewsReport>>();
        var otherPlayables = new HashSet<Guid>();
        foreach (var chunk in reportIds.Chunk(LookupChunkSize))
        {
            var ids = chunk.ToList();
            await foreach (var report in newsReportRepository.GetAllBy(item => ids.Contains(item.Id))
                               .WithCancellation(cancellationToken))
            {
                if (!reportsById.TryGetValue(report.Id, out var copies))
                {
                    reportsById[report.Id] = copies = [];
                }

                copies.Add(report);
            }

            await foreach (var tvEpisode in tvShowEpisodeRepository.GetAllBy(item => ids.Contains(item.Id))
                               .WithCancellation(cancellationToken))
            {
                otherPlayables.Add(tvEpisode.Id);
            }

            await foreach (var film in filmRepository.GetAllBy(item => ids.Contains(item.Id))
                               .WithCancellation(cancellationToken))
            {
                otherPlayables.Add(film.Id);
            }
        }

        var toMove = new List<NewsReport>();
        var alreadyMoved = new List<Guid>();
        var alreadyMovedReports = new List<NewsReport>();
        var notFound = new List<Guid>();
        var conflicts = new List<Guid>();
        foreach (var id in reportIds)
        {
            var copies = reportsById.GetValueOrDefault(id) ?? [];
            var inSource = copies.FirstOrDefault(report => report.NewsOrganisationId == sourceId);
            var inTarget = copies.FirstOrDefault(report => report.NewsOrganisationId == targetId);
            var elsewhere = copies.Any(report =>
                report.NewsOrganisationId != sourceId && report.NewsOrganisationId != targetId);
            if (otherPlayables.Contains(id) || elsewhere)
            {
                conflicts.Add(id);
            }
            else if (inSource is not null)
            {
                // Also when a copy is already in the target (an interrupted move): the source row is rewritten over it.
                toMove.Add(inSource);
            }
            else if (inTarget is not null)
            {
                alreadyMoved.Add(id);
                alreadyMovedReports.Add(inTarget);
            }
            else
            {
                notFound.Add(id);
            }
        }

        var duplicates = await DuplicateVideosInTarget(targetId, toMove, cancellationToken);
        conflicts.AddRange(duplicates);
        toMove.RemoveAll(report => duplicates.Contains(report.Id));
        return new MovePlan(toMove, alreadyMoved, alreadyMovedReports, notFound, conflicts, duplicates.Count);
    }

    /// <summary>
    /// Ids of reports to move whose YouTube video the target already has as a different report (same
    /// <c>youTubeId</c>, another id). Moving them would leave two reports for one video in one organisation.
    /// </summary>
    private async Task<List<Guid>> DuplicateVideosInTarget(
        Guid targetId,
        IReadOnlyList<NewsReport> toMove,
        CancellationToken cancellationToken)
    {
        var videoIds = toMove
            .Select(report => report.YouTubeId)
            .OfType<string>()
            .Distinct()
            .ToList();
        if (videoIds.Count == 0)
        {
            return [];
        }

        var inTarget = new List<(string YouTubeId, Guid Id)>();
        foreach (var chunk in videoIds.Chunk(LookupChunkSize))
        {
            var chunkIds = chunk.ToList();
            await foreach (var existing in newsReportRepository
                               .GetAllBy(item => item.NewsOrganisationId == targetId && chunkIds.Contains(item.YouTubeId!))
                               .WithCancellation(cancellationToken))
            {
                if (existing.YouTubeId is { } videoId)
                {
                    inTarget.Add((videoId, existing.Id));
                }
            }
        }

        return toMove
            .Where(report => report.YouTubeId is { } videoId &&
                             inTarget.Any(existing => existing.YouTubeId == videoId && existing.Id != report.Id))
            .Select(report => report.Id)
            .ToList();
    }

    private async Task SaveReports(IReadOnlyList<NewsReport> reports) =>
        await Parallel.ForEachAsync(
            reports,
            new ParallelOptions { MaxDegreeOfParallelism = CatalogueBulkWrite.Parallelism },
            async (report, _) => await CatalogueBulkWrite.RetryThrottled(() => newsReportRepository.Save(report)));

    /// <summary>
    /// Deletes each source row (the repository delete is idempotent, so a row already gone counts as deleted). Returns
    /// the ids deleted; a failed delete is logged and left out, so the caller reports only what really moved.
    /// </summary>
    private async Task<HashSet<Guid>> DeleteSourceRows(Guid sourceId, IReadOnlyList<NewsReport> reports)
    {
        var deleted = new ConcurrentBag<Guid>();
        await Parallel.ForEachAsync(
            reports,
            new ParallelOptions { MaxDegreeOfParallelism = CatalogueBulkWrite.Parallelism },
            async (report, _) =>
            {
                try
                {
                    await CatalogueBulkWrite.RetryThrottled(() => newsReportRepository.Delete(sourceId, report.Id));
                    deleted.Add(report.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "{method}: Could not delete source row of news report '{reportId}' in news organisation '{sourceId}'.",
                        nameof(DeleteSourceRows), report.Id, sourceId);
                }
            });
        return [.. deleted];
    }

    /// <summary>
    /// Same document shape as the news report edit and the news-report pull indexer (same id, the target's name as
    /// series), so the next pull run does not rewrite it. A report search excludes (a removed report, a parent-removed
    /// report or a removed target; ignored reports stay in search) is deleted from the index instead.
    /// </summary>
    private async Task<bool> SwapSearch(
        NewsOrganisation target,
        IReadOnlyList<NewsReport> reports,
        CancellationToken cancellationToken)
    {
        var excluded = reports
            .Where(report => CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(target, report))
            .Select(report => report.Id)
            .ToList();
        var documents = reports
            .Where(report => !excluded.Contains(report.Id))
            .Select(CataloguePlayableSearchDocument.For)
            .ToList();
        var uploaded = documents.Count == 0 || await searchSwap.UploadAsync(documents, cancellationToken);
        var deleted = excluded.Count == 0 || await searchSwap.DeleteAsync(excluded, cancellationToken);
        return uploaded && deleted;
    }

    /// <summary>Patches the target's <c>latestReleased</c> to the newest moved report (never lowered). Best effort.</summary>
    private async Task RaiseLatestReleased(NewsOrganisation target, IEnumerable<NewsReport> moved)
    {
        var released = moved.Select(report => report.ReleaseSort).DefaultIfEmpty().Max();
        if (released == default || (target.LatestReleased is { } latest && latest >= released))
        {
            return;
        }

        try
        {
            if (!await newsOrganisationRepository.RaiseLatestReleased(target.Id, released))
            {
                logger.LogWarning("{method}: News organisation '{organisationId}' was not found to patch latestReleased.",
                    nameof(RaiseLatestReleased), target.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: latestReleased patch failed for news organisation '{organisationId}'.",
                nameof(RaiseLatestReleased), target.Id);
        }
    }

    /// <summary>
    /// Moves the source's main YouTube channel (with its playlist, if the main channel owns it) and its additional
    /// channels into the target's <c>additionalYouTubeChannels</c>, carrying each channel's indexer-owned flags. Both
    /// organisations are re-read first. The source is saved with its channels cleared before the target is saved with
    /// them, so a channel is never claimed by both. If the target save fails the source's channels are restored (and
    /// logged in full at Error), so a re-send retries. A channel the target already holds is not added twice. Returns
    /// the channel ids added to the target.
    /// </summary>
    private async Task<ChannelMoveOutcome> MoveYouTubeChannels(Guid sourceId, Guid targetId)
    {
        NewsOrganisation? source;
        NewsOrganisation? target;
        try
        {
            source = await newsOrganisationRepository.GetNewsOrganisation(sourceId);
            target = await newsOrganisationRepository.GetNewsOrganisation(targetId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Could not read news organisation '{sourceId}' or '{targetId}'.",
                nameof(MoveYouTubeChannels), sourceId, targetId);
            return ChannelMoveOutcome.Fail(ChannelMoveRetryError);
        }

        if (source is null || target is null)
        {
            logger.LogWarning(
                "{method}: News organisation '{sourceId}' or '{targetId}' is gone; no YouTube channel moved.",
                nameof(MoveYouTubeChannels), sourceId, targetId);
            return source is null ? ChannelMoveOutcome.None : ChannelMoveOutcome.Fail(ChannelMoveRetryError);
        }

        var indexing = source.Indexing;
        var channels = new List<AdditionalYouTubeChannel>();
        if (indexing?.YouTubeChannelId is { } mainChannel)
        {
            if (indexing.YouTubePlaylistId is not null &&
                indexing.YouTubePlaylistOwnerChannelId is { } owner &&
                owner != mainChannel)
            {
                logger.LogWarning(
                    "{method}: News organisation '{sourceId}' main channel '{channelId}' indexes playlist '{playlistId}' owned by channel '{ownerId}'; no YouTube channel moved.",
                    nameof(MoveYouTubeChannels), sourceId, mainChannel, indexing.YouTubePlaylistId, owner);
                return ChannelMoveOutcome.Fail(ForeignPlaylistError);
            }

            var entry = AdditionalYouTubeChannel.ForPlaylist(mainChannel, indexing.YouTubePlaylistId);
            entry.YouTubePlaylistQueryIsExpensive = indexing.YouTubePlaylistQueryIsExpensive;
            entry.YouTubeChannelSearchForbidden = indexing.YouTubeChannelSearchForbidden;
            channels.Add(entry);
        }

        channels.AddRange(source.AdditionalYouTubeChannels?.Select(channel => channel.Copy()) ?? []);
        if (channels.Count == 0)
        {
            logger.LogInformation("{method}: News organisation '{sourceId}' has no YouTube channel to move.",
                nameof(MoveYouTubeChannels), sourceId);
            return ChannelMoveOutcome.None;
        }

        var toAdd = channels
            .Where(channel => target.Indexing?.YouTubeChannelId != channel.YouTubeChannelId &&
                              !target.HoldsAdditionalYouTubeChannel(channel.YouTubeChannelId))
            .ToList();

        // Snapshot by reference: the fields are set to null below, never mutated, so these restore them exactly.
        var originalAdditional = source.AdditionalYouTubeChannels;
        var original = indexing is null
            ? default
            : (indexing.YouTubeChannelId, indexing.YouTubePlaylistId, indexing.YouTubePlaylistOwnerChannelId,
                indexing.YouTubePlaylistIdHistory, indexing.YouTubePlaylistOrder,
                indexing.YouTubePlaylistQueryIsExpensive, indexing.YouTubeIndexingHints,
                indexing.YouTubeChannelSearchForbidden);
        var originalPayload = JsonSerializer.Serialize(new
        {
            sourceNewsOrganisationId = sourceId,
            youTubeChannelId = original.Item1,
            youTubePlaylistId = original.Item2,
            youTubePlaylistOwnerChannelId = original.Item3,
            youTubePlaylistIdHistory = original.Item4,
            youTubePlaylistOrder = original.Item5,
            youTubePlaylistQueryIsExpensive = original.Item6,
            youTubeIndexingHints = original.Item7,
            youTubeChannelSearchForbidden = original.Item8,
            additionalYouTubeChannels = originalAdditional
        });

        void Restore()
        {
            if (source.Indexing is { } restored)
            {
                (restored.YouTubeChannelId, restored.YouTubePlaylistId, restored.YouTubePlaylistOwnerChannelId,
                    restored.YouTubePlaylistIdHistory, restored.YouTubePlaylistOrder,
                    restored.YouTubePlaylistQueryIsExpensive, restored.YouTubeIndexingHints,
                    restored.YouTubeChannelSearchForbidden) = original;
            }

            source.AdditionalYouTubeChannels = originalAdditional;
        }

        if (indexing is not null)
        {
            indexing.YouTubeChannelId = null;
            indexing.YouTubePlaylistId = null;
            indexing.YouTubePlaylistOwnerChannelId = null;
            indexing.YouTubePlaylistIdHistory = null;
            indexing.YouTubePlaylistOrder = null;
            indexing.YouTubePlaylistQueryIsExpensive = null;
            indexing.YouTubeIndexingHints = null;
            indexing.YouTubeChannelSearchForbidden = null;
        }

        source.AdditionalYouTubeChannels = null;
        try
        {
            await newsOrganisationRepository.Save(source);
        }
        catch (Exception ex)
        {
            Restore();
            logger.LogError(ex,
                "{method}: Clearing the YouTube channels of news organisation '{sourceId}' failed; nothing changed. Original channels: {payload}",
                nameof(MoveYouTubeChannels), sourceId, originalPayload);
            return ChannelMoveOutcome.Fail(ChannelMoveRetryError);
        }

        if (toAdd.Count > 0)
        {
            try
            {
                target.AdditionalYouTubeChannels = [.. target.AdditionalYouTubeChannels ?? [], .. toAdd];
                await newsOrganisationRepository.Save(target);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "{method}: Saving the YouTube channels on news organisation '{targetId}' failed; restoring them on '{sourceId}'. Original channels: {payload}",
                    nameof(MoveYouTubeChannels), targetId, sourceId, originalPayload);
                Restore();
                try
                {
                    await newsOrganisationRepository.Save(source);
                    return ChannelMoveOutcome.Fail(ChannelMoveRetryError);
                }
                catch (Exception restoreEx)
                {
                    logger.LogError(restoreEx,
                        "{method}: Restoring the YouTube channels of news organisation '{sourceId}' failed; they are on neither organisation. Set them by hand from: {payload}",
                        nameof(MoveYouTubeChannels), sourceId, originalPayload);
                    return ChannelMoveOutcome.Fail(ChannelRestoreFailedError);
                }
            }
        }

        var added = toAdd.Select(channel => channel.YouTubeChannelId).ToList();
        logger.LogInformation(
            "{method}: Moved YouTube channel(s) of news organisation '{sourceId}' to '{targetId}'; added {added} to its additional channels ({skipped} already held).",
            nameof(MoveYouTubeChannels), sourceId, targetId, string.Join(", ", added), channels.Count - added.Count);
        return new ChannelMoveOutcome(added, false, null);
    }

    private async Task<int?> CountRemaining(Guid sourceId)
    {
        try
        {
            return await newsReportRepository.Count(sourceId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{method}: Could not count the reports left in news organisation '{organisationId}'.",
                nameof(CountRemaining), sourceId);
            return null;
        }
    }

    private static NewsReportOrganisationMoveResult? Validate(NewsReportOrganisationMoveCommand command)
    {
        if (command.TargetNewsOrganisationId == Guid.Empty)
        {
            return BadRequest("Give targetNewsOrganisationId");
        }

        if (command.TargetNewsOrganisationId == command.SourceNewsOrganisationId)
        {
            return BadRequest("The target news organisation is the source");
        }

        if (command.ReportIds is { } ids)
        {
            if (ids.Count == 0)
            {
                return BadRequest("reportIds cannot be empty. Omit it to move the source's next reports");
            }

            if (ids.Any(id => id == Guid.Empty))
            {
                return BadRequest("reportIds cannot contain an empty id");
            }

            if (ids.Distinct().Count() > NewsReportOrganisationMoveLimits.MaxReportsPerRequest)
            {
                return BadRequest(
                    $"At most {NewsReportOrganisationMoveLimits.MaxReportsPerRequest} report ids per request. Send the rest in another request");
            }
        }

        return null;
    }

    private static NewsReportOrganisationMoveResult Failed(
        Guid sourceId,
        NewsOrganisation target,
        IReadOnlyList<Guid> moved,
        IReadOnlyList<Guid> alreadyMoved,
        bool indexing,
        string? error = null) =>
        new(NewsReportOrganisationMoveStatus.Failed, sourceId, target.Id, target.Name.Trim(), moved, alreadyMoved, [], [],
            Error: error ?? "The move did not finish. Reports not yet moved are still in the source; re-send to resume",
            FailureIndexing: indexing);

    private static NewsReportOrganisationMoveResult NotFound(string error) =>
        new(NewsReportOrganisationMoveStatus.NotFound, Error: error);

    private static NewsReportOrganisationMoveResult BadRequest(string error) =>
        new(NewsReportOrganisationMoveStatus.BadRequest, Error: error);
}
