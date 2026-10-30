using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;

namespace CataloguePlayableServicesBackfill;

public sealed record CataloguePlayableServicesBackfillRunResult(
    int ExitCode,
    int Scanned,
    int Identical,
    int MissingTruth,
    int Discrepancies,
    int Applied,
    int SearchUploaded,
    int FailedWrites)
{
    /// <summary>youTubeId null to id.</summary>
    public int YouTubeIdFilled { get; init; }

    /// <summary>youTubeId idA to idB (url wins).</summary>
    public int YouTubeIdOverwritten { get; init; }

    /// <summary>youTubeId id to null. Always 0: a missing or unparseable url never clears an id.</summary>
    public int YouTubeIdCleared { get; init; }

    /// <summary>Url missing or not a video url; any existing id kept and logged.</summary>
    public int YouTubeIdSkippedUnparseable { get; init; }

    /// <summary>Apply found the document gone (deleted or moved mid-run); nothing written.</summary>
    public int SkippedNotFound { get; init; }

    /// <summary>Apply found the document changed since the scan (ETag / field mismatch); nothing written.</summary>
    public int SkippedConflict { get; init; }
}

/// <summary>
/// Cosmos-first repair: scan Film / TvShowEpisode / NewsReport, diff against episode backup JSON
/// and/or PublicDatabase urls, carry service url/image discrepancies onto the live entity, and sync the compact
/// <c>youTubeId</c> from <c>services.youtube.url</c> (url wins; never cleared). Dry-run by default.
/// <para>
/// Apply never replaces a whole news report or TV episode: it re-reads the document with its ETag, checks the
/// fields it scanned are unchanged, then sends a Cosmos partial patch of only <c>/services</c> and/or
/// <c>/youTubeId</c> guarded by that ETag. A document deleted or moved mid-run is skipped (patch never upserts);
/// one edited mid-run is skipped as a conflict. Films use the ETag-guarded <see cref="IFilmRepository.Update"/>.
/// Search documents are pushed in batches of <see cref="SearchBatchSize"/> (films have no search pull, so the
/// push is required for them; news/TV are re-pulled anyway because the write bumps <c>_ts</c>).
/// </para>
/// </summary>
public class CataloguePlayableServicesBackfillProcessor(
    IFilmRepository filmRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsReportRepository newsReportRepository,
    IEpisodeBackupStore backupStore,
    Func<string, IPublicDatabaseUrlIndex> publicDatabaseUrlIndexFactory,
    Func<IPlayableSearchDocumentSwap> searchSwapFactory,
    TimeProvider timeProvider,
    ILogger<CataloguePlayableServicesBackfillProcessor> logger)
{
    public const int ExitOk = 0;
    public const int ExitBadArguments = 1;
    public const int ExitApplyRefused = 2;
    public const int ExitFailed = 3;
    public const int SearchBatchSize = 100;

    public const string YouTubeIdFilled = "filled";
    public const string YouTubeIdOverwritten = "overwritten";
    public const string YouTubeIdCleared = "cleared";
    public const string YouTubeIdSkippedUnparseable = "skipped-unparseable";
    public const string SkippedNotFound = "not-found";
    public const string SkippedConflict = "conflict";

    public async Task<CataloguePlayableServicesBackfillRunResult> Run(
        CataloguePlayableServicesBackfillRequest request,
        CancellationToken cancellationToken = default)
    {
        var hasEpisodeBackup = !request.YouTubeIdOnly && !string.IsNullOrWhiteSpace(request.EpisodeBackupDir);
        var hasPublicDatabase = !request.YouTubeIdOnly && !string.IsNullOrWhiteSpace(request.PublicDatabaseDir);
        if (!request.YouTubeIdOnly && !hasEpisodeBackup && !hasPublicDatabase)
        {
            logger.LogError(
                "CataloguePlayableServicesBackfill requires --episode-backup-dir and/or --public-database-dir (or --youtube-id-only).");
            return Empty(ExitBadArguments);
        }

        if (!PlayableKindParser.TryParseSelection(request.Kind, out var kinds))
        {
            logger.LogError(
                "CataloguePlayableServicesBackfill --kind must be film, tvshowepisode, newsreport, or all. Got {Kind}.",
                request.Kind);
            return Empty(ExitBadArguments);
        }

        if (request.Apply && !CataloguePlayableServicesBackfillConsent.IsApplyConsented(request))
        {
            logger.LogError(
                "CataloguePlayableServicesBackfill --apply is refused (exit 2): --consent is missing or is not the exact phrase. Agents must never supply consent.");
            return Empty(ExitApplyRefused);
        }

        if (request.Limit < 0)
        {
            logger.LogError("CataloguePlayableServicesBackfill --limit must be >= 0.");
            return Empty(ExitBadArguments);
        }

        IPublicDatabaseUrlIndex publicIndex = EmptyPublicDatabaseUrlIndex.Instance;
        if (hasPublicDatabase)
        {
            try
            {
                publicIndex = publicDatabaseUrlIndexFactory(request.PublicDatabaseDir!);
            }
            catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
            {
                logger.LogError(ex, "CataloguePlayableServicesBackfill failed to load --public-database-dir.");
                return Empty(ExitBadArguments);
            }
        }

        HashSet<Guid>? idFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Ids))
        {
            idFilter = [];
            foreach (var part in request.Ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(part, out var id))
                {
                    logger.LogError("CataloguePlayableServicesBackfill --ids contains an invalid Guid: {Part}.", part);
                    return Empty(ExitBadArguments);
                }

                idFilter.Add(id);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SnapshotDir))
        {
            Directory.CreateDirectory(request.SnapshotDir);
        }

        await using var changeLog = new JsonlChangeLogWriter(request.ChangeLog);
        var counters = new Counters();
        var pendingSearch = new List<EpisodeSearchRecord>();
        var progressEvery = Math.Max(1, request.ProgressEvery);

        foreach (var kind in kinds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = kind switch
            {
                PlayableKind.Film => Adapt(filmRepository.GetAll(), FilmAdapter),
                PlayableKind.TvShowEpisode => Adapt(tvShowEpisodeRepository.GetAll(), TvAdapter),
                _ => Adapt(newsReportRepository.GetAll(), NewsAdapter)
            };

            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                if (!Include(item.Id, idFilter))
                {
                    continue;
                }

                counters.Scanned++;
                LogProgress(counters.Scanned, progressEvery);
                await ProcessOne(request, publicIndex, changeLog, item, counters, pendingSearch, cancellationToken);
                if (pendingSearch.Count >= SearchBatchSize)
                {
                    await FlushSearch(pendingSearch, counters, cancellationToken);
                }

                if (request.Limit > 0 && counters.Discrepancies >= request.Limit)
                {
                    await FlushSearch(pendingSearch, counters, cancellationToken);
                    return Finish(counters);
                }
            }
        }

        await FlushSearch(pendingSearch, counters, cancellationToken);
        return Finish(counters);
    }

    private static async IAsyncEnumerable<PlayableAdapter> Adapt<T>(
        IAsyncEnumerable<T> source,
        Func<T, PlayableAdapter> adapt)
    {
        await foreach (var item in source)
        {
            yield return adapt(item);
        }
    }

    private PlayableAdapter FilmAdapter(Film film) =>
        new(PlayableKind.Film, film.Id, null, film.Services, film,
            async plan =>
            {
                var saved = await filmRepository.Update(film.Id, f =>
                {
                    if (plan.ServicesChanged)
                    {
                        f.Services = plan.Services;
                    }

                    EpisodeServicePresence.SyncCatalogueYouTubeIdFromUrl(f);
                    return true;
                });
                if (saved is null)
                {
                    return WriteResult.Failed;
                }

                return WriteResult.Written(CataloguePlayableSearchDocument.ShouldExcludeFromSearch(saved)
                    ? null
                    : CataloguePlayableSearchDocument.For(saved));
            });

    private PlayableAdapter TvAdapter(TvShowEpisode episode)
    {
        var scannedServices = ServicesJson(episode.Services);
        var scannedYouTubeId = NormaliseId(episode.YouTubeId);
        return new PlayableAdapter(PlayableKind.TvShowEpisode, episode.Id, episode.TvShowId, episode.Services, episode,
            async plan =>
            {
                var fresh = await tvShowEpisodeRepository.GetTvShowEpisodeWithETag(episode.TvShowId, episode.Id);
                if (fresh is null)
                {
                    return WriteResult.NotFound;
                }

                if (!Unchanged(fresh.Item, scannedServices, scannedYouTubeId))
                {
                    return WriteResult.Conflict;
                }

                var outcome = await tvShowEpisodeRepository.PatchFields(
                    episode.TvShowId, episode.Id, fresh.ETag, plan.ToPatch());
                if (outcome != CataloguePlayablePatchOutcome.Patched)
                {
                    return WriteResult.From(outcome);
                }

                plan.ApplyTo(fresh.Item);
                return WriteResult.Written(CataloguePlayableSearchDocument.ShouldExcludeFromSearch(fresh.Item)
                    ? null
                    : CataloguePlayableSearchDocument.For(fresh.Item));
            });
    }

    private PlayableAdapter NewsAdapter(NewsReport report)
    {
        var scannedServices = ServicesJson(report.Services);
        var scannedYouTubeId = NormaliseId(report.YouTubeId);
        return new PlayableAdapter(PlayableKind.NewsReport, report.Id, report.NewsOrganisationId, report.Services,
            report,
            async plan =>
            {
                var fresh = await newsReportRepository.GetNewsReportWithETag(report.NewsOrganisationId, report.Id);
                if (fresh is null)
                {
                    return WriteResult.NotFound;
                }

                if (!Unchanged(fresh.Item, scannedServices, scannedYouTubeId))
                {
                    return WriteResult.Conflict;
                }

                var outcome = await newsReportRepository.PatchFields(
                    report.NewsOrganisationId, report.Id, fresh.ETag, plan.ToPatch());
                if (outcome != CataloguePlayablePatchOutcome.Patched)
                {
                    return WriteResult.From(outcome);
                }

                plan.ApplyTo(fresh.Item);
                return WriteResult.Written(CataloguePlayableSearchDocument.ShouldExcludeFromSearch(fresh.Item)
                    ? null
                    : CataloguePlayableSearchDocument.For(fresh.Item));
            });
    }

    /// <summary>
    /// The fields this run derives its write from must still be what was scanned; otherwise someone edited the
    /// document mid-run and the planned value may be stale.
    /// </summary>
    private static bool Unchanged(ICatalogueYouTubeItem current, string scannedServices, string? scannedYouTubeId) =>
        string.Equals(ServicesJson(current.Services), scannedServices, StringComparison.Ordinal) &&
        string.Equals(NormaliseId(current.YouTubeId), scannedYouTubeId, StringComparison.Ordinal);

    private static string ServicesJson(IReadOnlyDictionary<string, ServiceLink>? services) =>
        services is null
            ? "null"
            : JsonSerializer.Serialize(
                services.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => new { url = x.Value?.Url?.OriginalString, image = x.Value?.Image?.OriginalString }));

    private static string? NormaliseId(string? id) => string.IsNullOrWhiteSpace(id) ? null : id;

    private async Task ProcessOne(
        CataloguePlayableServicesBackfillRequest request,
        IPublicDatabaseUrlIndex publicIndex,
        IChangeLogWriter changeLog,
        PlayableAdapter item,
        Counters counters,
        List<EpisodeSearchRecord> pendingSearch,
        CancellationToken cancellationToken)
    {
        var kind = item.Kind;
        var id = item.Id;
        var parentId = item.ParentId;
        var liveServices = item.LiveServices;
        var scannedYouTubeId = NormaliseId(item.Target.YouTubeId);

        Dictionary<string, ServiceLink>? episodeBackup = null;
        string backupPath = string.Empty;
        if (!request.YouTubeIdOnly && !string.IsNullOrWhiteSpace(request.EpisodeBackupDir))
        {
            var resolved = backupStore.ResolvePath(request.EpisodeBackupDir, id);
            if (resolved is not null && backupStore.TryLoadServices(resolved, out var loaded))
            {
                backupPath = resolved;
                episodeBackup = loaded;
            }
        }

        PublicUrlHit? publicHit = null;
        if (!request.YouTubeIdOnly && publicIndex.TryGet(id, out var hit))
        {
            publicHit = hit;
        }

        // Compact youTubeId comes from the live services.youtube.url, so it never needs a truth source.
        // Playables created before youTubeId existed (often with no backup / PublicDatabase row) must still sync.
        var youTube = PlanYouTubeId(item.Target, liveServices, scannedYouTubeId);
        var hasTruth = !request.YouTubeIdOnly && (episodeBackup is not null || publicHit is not null);
        if (!hasTruth && !request.YouTubeIdOnly && !youTube.Changed)
        {
            CountYouTube(youTube, counters, kind, id);
            await changeLog.WriteAsync(new ChangeLogRecord
            {
                Kind = PlayableKindParser.ToWire(kind),
                Id = id,
                ParentId = parentId,
                BackupPath = backupPath,
                PublicDatabasePath = string.Empty,
                Changes = [],
                Applied = false,
                Utc = timeProvider.GetUtcNow(),
                MissingTruth = true,
                YouTubeIdOutcome = OutcomeWire(youTube)
            }, cancellationToken);
            counters.MissingTruth++;
            return;
        }

        var truth = hasTruth
            ? TruthServicesMerger.Merge(episodeBackup, publicHit?.Urls)
            : new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
        var carry = PlayableServicesDiscrepancyCarry.Apply(liveServices, truth);
        if (carry.HasChanges)
        {
            // A carried youtube url changes what the id must be: url wins, against the scanned id.
            youTube = PlanYouTubeId(item.Target, carry.Services, scannedYouTubeId);
        }

        CountYouTube(youTube, counters, kind, id);
        if (!carry.HasChanges && !youTube.Changed)
        {
            if (youTube.Outcome == CatalogueYouTubeIdSyncOutcome.SkippedUnparseable)
            {
                await WriteChange(changeLog, kind, id, parentId, backupPath, string.Empty, [], applied: false,
                    youTube, skipped: null, cancellationToken);
            }

            counters.Identical++;
            return;
        }

        counters.Discrepancies++;
        var changes = carry.HasChanges
            ? carry.Changes.ToList()
            : [];
        if (youTube.Changed)
        {
            changes.Add(new ServiceFieldChange(ServiceKeys.YouTube, "youTubeId", youTube.Before, youTube.After));
        }

        if (!string.IsNullOrWhiteSpace(request.SnapshotDir))
        {
            var beforePath = Path.Combine(
                request.SnapshotDir,
                $"{PlayableKindParser.ToWire(kind)}-{id:D}-services-before.json");
            await File.WriteAllTextAsync(
                beforePath,
                JsonSerializer.Serialize(new { services = liveServices, youTubeId = scannedYouTubeId },
                    BackupDocumentJsonOptions.Instance),
                cancellationToken);
        }

        var publicPath = publicHit?.FilePath ?? string.Empty;
        if (!request.Apply)
        {
            await WriteChange(changeLog, kind, id, parentId, backupPath, publicPath, changes, applied: false,
                youTube, skipped: null, cancellationToken);
            return;
        }

        WriteResult result;
        try
        {
            result = await item.Write(new PlannedWrite(
                carry.HasChanges,
                carry.HasChanges ? carry.Services : liveServices,
                youTube));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply services carry for {Kind} {Id}.", kind, id);
            result = WriteResult.Failed;
        }

        switch (result.Outcome)
        {
            case WriteOutcome.Written:
                counters.Applied++;
                if (result.SearchDocument is not null)
                {
                    pendingSearch.Add(result.SearchDocument);
                }

                break;
            case WriteOutcome.NotFound:
                counters.SkippedNotFound++;
                logger.LogWarning(
                    "Skipped {Kind} {Id}: it no longer exists (deleted or moved mid-run). Nothing was written.",
                    kind, id);
                break;
            case WriteOutcome.Conflict:
                counters.SkippedConflict++;
                logger.LogWarning(
                    "Skipped {Kind} {Id}: it changed after it was scanned. Nothing was written; re-run to retry.",
                    kind, id);
                break;
            default:
                counters.FailedWrites++;
                logger.LogError(
                    "Failed to save {Kind} {Id} after carrying services from backup={BackupPath} public={PublicPath}.",
                    kind, id, backupPath, publicPath);
                break;
        }

        await WriteChange(changeLog, kind, id, parentId, backupPath, publicPath, changes,
            applied: result.Outcome == WriteOutcome.Written,
            youTube,
            skipped: result.Outcome switch
            {
                WriteOutcome.NotFound => SkippedNotFound,
                WriteOutcome.Conflict => SkippedConflict,
                _ => null
            },
            cancellationToken);
    }

    /// <summary>
    /// What the url-wins, never-clear sync would do to <paramref name="target"/> with <paramref name="services"/>,
    /// measured against the id as scanned. The scanned object is updated in memory only.
    /// </summary>
    private static CatalogueYouTubeIdSyncResult PlanYouTubeId(
        ICatalogueYouTubeItem target,
        Dictionary<string, ServiceLink>? services,
        string? scannedYouTubeId)
    {
        target.Services = services;
        target.YouTubeId = scannedYouTubeId;
        return EpisodeServicePresence.SyncCatalogueYouTubeIdFromUrl(target);
    }

    private void CountYouTube(CatalogueYouTubeIdSyncResult youTube, Counters counters, PlayableKind kind, Guid id)
    {
        switch (youTube.Outcome)
        {
            case CatalogueYouTubeIdSyncOutcome.Filled:
                counters.YouTubeIdFilled++;
                break;
            case CatalogueYouTubeIdSyncOutcome.Overwritten:
                counters.YouTubeIdOverwritten++;
                break;
            case CatalogueYouTubeIdSyncOutcome.SkippedUnparseable:
                counters.YouTubeIdSkippedUnparseable++;
                logger.LogWarning(
                    "{Kind} {Id}: services.youtube.url is missing or not a video url; youTubeId {YouTubeId} kept (skipped-unparseable).",
                    kind, id, youTube.Before ?? "(none)");
                break;
        }
    }

    private static string? OutcomeWire(CatalogueYouTubeIdSyncResult youTube) => youTube.Outcome switch
    {
        CatalogueYouTubeIdSyncOutcome.Filled => YouTubeIdFilled,
        CatalogueYouTubeIdSyncOutcome.Overwritten => YouTubeIdOverwritten,
        CatalogueYouTubeIdSyncOutcome.SkippedUnparseable => YouTubeIdSkippedUnparseable,
        _ => null
    };

    private async Task FlushSearch(
        List<EpisodeSearchRecord> pending,
        Counters counters,
        CancellationToken cancellationToken)
    {
        if (pending.Count == 0)
        {
            return;
        }

        var batch = pending.ToList();
        pending.Clear();
        try
        {
            var swap = searchSwapFactory();
            if (await swap.UploadAsync(batch, cancellationToken))
            {
                counters.SearchUploaded += batch.Count;
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search upload of {Count} documents threw.", batch.Count);
        }

        logger.LogError(
            "Cosmos writes succeeded but the search upload of {Count} documents failed; the search pull re-reads news/TV on the next run (films need a re-run).",
            batch.Count);
    }

    private async Task WriteChange(
        IChangeLogWriter changeLog,
        PlayableKind kind,
        Guid id,
        Guid? parentId,
        string backupPath,
        string publicDatabasePath,
        IReadOnlyList<ServiceFieldChange> changes,
        bool applied,
        CatalogueYouTubeIdSyncResult youTube,
        string? skipped,
        CancellationToken cancellationToken) =>
        await changeLog.WriteAsync(new ChangeLogRecord
        {
            Kind = PlayableKindParser.ToWire(kind),
            Id = id,
            ParentId = parentId,
            BackupPath = backupPath,
            PublicDatabasePath = publicDatabasePath,
            Changes = changes,
            Applied = applied,
            Utc = timeProvider.GetUtcNow(),
            YouTubeIdOutcome = OutcomeWire(youTube),
            Skipped = skipped
        }, cancellationToken);

    private void LogProgress(int scanned, int progressEvery)
    {
        if (scanned % progressEvery == 0)
        {
            logger.LogInformation("CataloguePlayableServicesBackfill scanned {Scanned} playables…", scanned);
        }
    }

    private static bool Include(Guid id, HashSet<Guid>? filter) =>
        filter is null || filter.Contains(id);

    private CataloguePlayableServicesBackfillRunResult Finish(Counters c)
    {
        logger.LogInformation(
            "CataloguePlayableServicesBackfill done. Scanned={Scanned} Identical={Identical} MissingTruth={MissingTruth} Discrepancies={Discrepancies} YouTubeIdFilled={Filled} YouTubeIdOverwritten={Overwritten} YouTubeIdCleared={Cleared} YouTubeIdSkippedUnparseable={SkippedUnparseable} Applied={Applied} SkippedNotFound={SkippedNotFound} SkippedConflict={SkippedConflict} SearchUploaded={SearchUploaded} FailedWrites={FailedWrites}",
            c.Scanned, c.Identical, c.MissingTruth, c.Discrepancies, c.YouTubeIdFilled, c.YouTubeIdOverwritten,
            c.YouTubeIdCleared, c.YouTubeIdSkippedUnparseable, c.Applied, c.SkippedNotFound, c.SkippedConflict,
            c.SearchUploaded, c.FailedWrites);
        var exitCode = c.FailedWrites > 0 ? ExitFailed : ExitOk;
        return new CataloguePlayableServicesBackfillRunResult(
            exitCode, c.Scanned, c.Identical, c.MissingTruth, c.Discrepancies, c.Applied, c.SearchUploaded,
            c.FailedWrites)
        {
            YouTubeIdFilled = c.YouTubeIdFilled,
            YouTubeIdOverwritten = c.YouTubeIdOverwritten,
            YouTubeIdCleared = c.YouTubeIdCleared,
            YouTubeIdSkippedUnparseable = c.YouTubeIdSkippedUnparseable,
            SkippedNotFound = c.SkippedNotFound,
            SkippedConflict = c.SkippedConflict
        };
    }

    private static CataloguePlayableServicesBackfillRunResult Empty(int exitCode) =>
        new(exitCode, 0, 0, 0, 0, 0, 0, 0);

    private sealed class Counters
    {
        public int Scanned;
        public int Identical;
        public int MissingTruth;
        public int Discrepancies;
        public int Applied;
        public int SearchUploaded;
        public int FailedWrites;
        public int YouTubeIdFilled;
        public int YouTubeIdOverwritten;
        public int YouTubeIdCleared;
        public int YouTubeIdSkippedUnparseable;
        public int SkippedNotFound;
        public int SkippedConflict;
    }

    private sealed record PlayableAdapter(
        PlayableKind Kind,
        Guid Id,
        Guid? ParentId,
        Dictionary<string, ServiceLink>? LiveServices,
        ICatalogueYouTubeItem Target,
        Func<PlannedWrite, Task<WriteResult>> Write);

    private sealed record PlannedWrite(
        bool ServicesChanged,
        Dictionary<string, ServiceLink>? Services,
        CatalogueYouTubeIdSyncResult YouTube)
    {
        public CataloguePlayableFieldPatch ToPatch() => new()
        {
            SetServices = ServicesChanged,
            Services = ServicesChanged ? Services : null,
            SetYouTubeId = YouTube.Changed,
            YouTubeId = YouTube.Changed ? YouTube.After : null
        };

        public void ApplyTo(ICatalogueYouTubeItem item)
        {
            if (ServicesChanged)
            {
                item.Services = Services;
            }

            if (YouTube.Changed)
            {
                item.YouTubeId = YouTube.After;
            }
        }
    }

    private enum WriteOutcome
    {
        Written,
        NotFound,
        Conflict,
        Failed
    }

    private sealed record WriteResult(WriteOutcome Outcome, EpisodeSearchRecord? SearchDocument)
    {
        public static readonly WriteResult NotFound = new(WriteOutcome.NotFound, null);
        public static readonly WriteResult Conflict = new(WriteOutcome.Conflict, null);
        public static readonly WriteResult Failed = new(WriteOutcome.Failed, null);
        public static WriteResult Written(EpisodeSearchRecord? document) => new(WriteOutcome.Written, document);

        public static WriteResult From(CataloguePlayablePatchOutcome outcome) => outcome switch
        {
            CataloguePlayablePatchOutcome.NotFound => NotFound,
            CataloguePlayablePatchOutcome.Conflict => Conflict,
            _ => Failed
        };
    }
}
