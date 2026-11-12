using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
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
/// Order, so a failure never loses a report:
/// <list type="number">
/// <item>Check every requested id before any write: an id that is not in the source (and not already in the target), or
/// that is a report of a third organisation, a TV episode or a film, refuses the whole request.</item>
/// <item>Save each report into the target.</item>
/// <item>Swap each search document under the same id (now <c>seriesName</c> = the target's name). On failure the source
/// rows are kept.</item>
/// <item>Re-read the target and each saved report; a concurrent delete keeps the source rows.</item>
/// <item>Delete the moved source rows.</item>
/// <item>Raise the target's <c>latestReleased</c> (conditional patch, never lowered) and delete the moved reports'
/// short-link records, so the Shortner resolves each id from search and redirects to the target's page. Both best
/// effort.</item>
/// <item>With <c>moveYouTubeChannels</c>: move the source's main and additional YouTube channels into the target's
/// <c>additionalYouTubeChannels</c> (source cleared first, so no channel is ever claimed twice). A failure is reported
/// in the result and does not undo the report move; re-sending repeats it.</item>
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
    ILogger<NewsReportOrganisationMoveService> logger) : INewsReportOrganisationMoveService
{
    /// <summary>Ids per Cosmos <c>IN</c> lookup.</summary>
    private const int LookupChunkSize = 100;

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

            var requestedIds = command.ReportIds is { Count: > 0 } ids
                ? ids.Distinct().ToList()
                : await NextSourceReportIds(source.Id, cancellationToken);
            plan = await Plan(source.Id, target.Id, requestedIds, cancellationToken);
            if (plan.NotFound.Count > 0 || plan.Conflicts.Count > 0)
            {
                logger.LogWarning(
                    "{method}: Refused moving {requested} report(s) from news organisation '{sourceId}' to '{targetId}': {notFound} not in the source, {conflicts} already another playable. Nothing written.",
                    nameof(MoveAsync), requestedIds.Count, source.Id, target.Id, plan.NotFound.Count,
                    plan.Conflicts.Count);
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
                        : "Some report ids are a report of another organisation, a TV episode or a film. Nothing was moved");
            }

            IReadOnlyList<Guid> moved = [];
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
                    return Failed(source.Id, target, plan.AlreadyMoved, indexing: true);
                }

                // Last check before the only irreversible step: a concurrent delete of the target or of a copied report
                // would leave the report with nowhere to live once its source row is gone.
                if (await newsOrganisationRepository.GetNewsOrganisation(target.Id) is null)
                {
                    logger.LogError(
                        "{method}: Target news organisation '{targetId}' is gone (changed concurrently). Source rows kept; re-send to resume.",
                        nameof(MoveAsync), target.Id);
                    return Failed(source.Id, target, plan.AlreadyMoved, indexing: true);
                }

                foreach (var report in reports)
                {
                    if (await newsReportRepository.GetNewsReport(target.Id, report.Id) is null)
                    {
                        logger.LogError(
                            "{method}: News report '{reportId}' is gone from the target (changed concurrently). Source rows kept; re-send to resume.",
                            nameof(MoveAsync), report.Id);
                        return Failed(source.Id, target, plan.AlreadyMoved, indexing: true);
                    }
                }

                await DeleteSourceRows(source.Id, reports);
                moved = reports.Select(report => report.Id).ToList();
                logger.LogInformation(
                    "Moved {count} news report(s) from news organisation '{sourceName}' ('{sourceId}') to '{targetName}' ('{targetId}').",
                    moved.Count, source.Name, source.Id, target.Name, target.Id);
                await RaiseLatestReleased(target, reports);
                foreach (var report in reports)
                {
                    await NewsReportMoveSource.DeleteShortLink(shortnerService, logger, report);
                }
            }
            else
            {
                logger.LogInformation(
                    "{method}: Nothing left to move from news organisation '{sourceId}' to '{targetId}' ({alreadyMoved} already moved).",
                    nameof(MoveAsync), source.Id, target.Id, plan.AlreadyMoved.Count);
            }

            IReadOnlyList<string> movedChannels = [];
            var channelMoveFailed = false;
            if (command.MoveYouTubeChannels)
            {
                (movedChannels, channelMoveFailed) = await MoveYouTubeChannels(source.Id, target.Id);
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
                Error: channelMoveFailed
                    ? "The reports moved, but moving the YouTube channels failed. Re-send with moveYouTubeChannels to retry"
                    : null)
            {
                RemainingReportCount = remaining,
                EmptiedNewsOrganisationId = emptied,
                MovedYouTubeChannelIds = movedChannels,
                YouTubeChannelMoveFailed = channelMoveFailed
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed moving reports from news organisation '{sourceId}' to '{targetId}'. Source rows kept; re-send to resume.",
                nameof(MoveAsync), command.SourceNewsOrganisationId, command.TargetNewsOrganisationId);
            return target is null
                ? new NewsReportOrganisationMoveResult(NewsReportOrganisationMoveStatus.Failed,
                    Error: "Unable to move the reports. The source reports were kept; re-send to resume")
                : Failed(command.SourceNewsOrganisationId, target, plan?.AlreadyMoved ?? [], indexing: false);
        }
    }

    private sealed record MovePlan(
        IReadOnlyList<NewsReport> ToMove,
        IReadOnlyList<Guid> AlreadyMoved,
        IReadOnlyList<Guid> NotFound,
        IReadOnlyList<Guid> Conflicts);

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
        var notFound = new List<Guid>();
        var conflicts = new List<Guid>();
        foreach (var id in reportIds)
        {
            var copies = reportsById.GetValueOrDefault(id) ?? [];
            var inSource = copies.FirstOrDefault(report => report.NewsOrganisationId == sourceId);
            var inTarget = copies.Any(report => report.NewsOrganisationId == targetId);
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
            else if (inTarget)
            {
                alreadyMoved.Add(id);
            }
            else
            {
                notFound.Add(id);
            }
        }

        return new MovePlan(toMove, alreadyMoved, notFound, conflicts);
    }

    private async Task SaveReports(IReadOnlyList<NewsReport> reports) =>
        await Parallel.ForEachAsync(
            reports,
            new ParallelOptions { MaxDegreeOfParallelism = CatalogueBulkWrite.Parallelism },
            async (report, _) => await CatalogueBulkWrite.RetryThrottled(() => newsReportRepository.Save(report)));

    private async Task DeleteSourceRows(Guid sourceId, IReadOnlyList<NewsReport> reports) =>
        await Parallel.ForEachAsync(
            reports,
            new ParallelOptions { MaxDegreeOfParallelism = CatalogueBulkWrite.Parallelism },
            async (report, _) =>
                await CatalogueBulkWrite.RetryThrottled(() => newsReportRepository.Delete(sourceId, report.Id)));

    /// <summary>
    /// Same document shape as the news report edit and the news-report pull indexer (same id, the target's name as
    /// series), so the next pull run does not rewrite it. A report search excludes (removed or ignored, or a removed
    /// target) is deleted from the index instead.
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

    /// <summary>Raises the target's <c>latestReleased</c> to the newest moved report (never lowered). Best effort.</summary>
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
    /// Moves the source's main YouTube channel (with its playlist, if any) and its additional channels into the
    /// target's <c>additionalYouTubeChannels</c>, carrying each channel's indexer-owned flags. Both organisations are
    /// re-read first. The source is saved with its channels cleared before the target is saved with them, so a channel
    /// is never claimed by both (the claim checks would then refuse later edits of either). A channel the target already
    /// holds is not added twice, so a re-send after a partial failure finishes the move. Returns the channel ids moved.
    /// </summary>
    private async Task<(IReadOnlyList<string> Moved, bool Failed)> MoveYouTubeChannels(Guid sourceId, Guid targetId)
    {
        try
        {
            var source = await newsOrganisationRepository.GetNewsOrganisation(sourceId);
            var target = await newsOrganisationRepository.GetNewsOrganisation(targetId);
            if (source is null || target is null)
            {
                logger.LogWarning(
                    "{method}: News organisation '{sourceId}' or '{targetId}' is gone; no YouTube channel moved.",
                    nameof(MoveYouTubeChannels), sourceId, targetId);
                return ([], source is not null);
            }

            var channels = new List<AdditionalYouTubeChannel>();
            if (source.Indexing?.YouTubeChannelId is { } mainChannel)
            {
                var playlistOnChannel = source.Indexing.YouTubePlaylistId is { } playlist &&
                                        (source.Indexing.YouTubePlaylistOwnerChannelId is null ||
                                         source.Indexing.YouTubePlaylistOwnerChannelId == mainChannel)
                    ? playlist
                    : null;
                var entry = AdditionalYouTubeChannel.ForPlaylist(mainChannel, playlistOnChannel);
                entry.YouTubePlaylistQueryIsExpensive = source.Indexing.YouTubePlaylistQueryIsExpensive;
                entry.YouTubeChannelSearchForbidden = source.Indexing.YouTubeChannelSearchForbidden;
                channels.Add(entry);
            }

            channels.AddRange(source.AdditionalYouTubeChannels?.Select(channel => channel.Copy()) ?? []);
            if (channels.Count == 0)
            {
                logger.LogInformation("{method}: News organisation '{sourceId}' has no YouTube channel to move.",
                    nameof(MoveYouTubeChannels), sourceId);
                return ([], false);
            }

            var toAdd = channels
                .Where(channel => target.Indexing?.YouTubeChannelId != channel.YouTubeChannelId &&
                                  !target.HoldsAdditionalYouTubeChannel(channel.YouTubeChannelId))
                .ToList();

            if (source.Indexing is { } indexing)
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
            await newsOrganisationRepository.Save(source);
            if (toAdd.Count > 0)
            {
                target.AdditionalYouTubeChannels = [.. target.AdditionalYouTubeChannels ?? [], .. toAdd];
                await newsOrganisationRepository.Save(target);
            }

            var moved = channels.Select(channel => channel.YouTubeChannelId).ToList();
            logger.LogInformation(
                "{method}: Moved YouTube channel(s) {channels} from news organisation '{sourceId}' to the additional channels of '{targetId}'.",
                nameof(MoveYouTubeChannels), string.Join(", ", moved), sourceId, targetId);
            return (moved, false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Moving YouTube channels from news organisation '{sourceId}' to '{targetId}' failed. Re-send to retry.",
                nameof(MoveYouTubeChannels), sourceId, targetId);
            return ([], true);
        }
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
        IReadOnlyList<Guid> alreadyMoved,
        bool indexing) =>
        new(NewsReportOrganisationMoveStatus.Failed, sourceId, target.Id, target.Name.Trim(), [], alreadyMoved, [], [],
            Error: "The move did not finish. The source reports were kept; re-send to resume",
            FailureIndexing: indexing);

    private static NewsReportOrganisationMoveResult NotFound(string error) =>
        new(NewsReportOrganisationMoveStatus.NotFound, Error: error);

    private static NewsReportOrganisationMoveResult BadRequest(string error) =>
        new(NewsReportOrganisationMoveStatus.BadRequest, Error: error);
}
