using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace CataloguePlayableServicesBackfill;

public sealed record CataloguePlayableServicesBackfillRunResult(
    int ExitCode,
    int Scanned,
    int Identical,
    int MissingTruth,
    int Discrepancies,
    int Applied,
    int SearchUploaded,
    int FailedWrites);

/// <summary>
/// Cosmos-first repair: scan Film / TvShowEpisode / NewsReport, diff against episode backup JSON
/// and/or PublicDatabase urls, carry service url/image discrepancies onto the live entity.
/// Dry-run by default.
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

        var scanned = 0;
        var identical = 0;
        var missingTruth = 0;
        var discrepancies = 0;
        var applied = 0;
        var searchUploaded = 0;
        var failedWrites = 0;
        var progressEvery = Math.Max(1, request.ProgressEvery);

        foreach (var kind in kinds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (kind)
            {
                case PlayableKind.Film:
                    await foreach (var film in filmRepository.GetAll().WithCancellation(cancellationToken))
                    {
                        if (!Include(film.Id, idFilter))
                        {
                            continue;
                        }

                        scanned++;
                        LogProgress(scanned, progressEvery);
                        Film? savedFilm = null;
                        var outcome = await ProcessOne(
                            request,
                            publicIndex,
                            changeLog,
                            PlayableKind.Film,
                            film.Id,
                            parentId: null,
                            film.Services,
                            async services =>
                            {
                                savedFilm = await filmRepository.Update(film.Id, f =>
                                {
                                    f.Services = services;
                                    EpisodeServicePresence.SyncCatalogueYouTubeId(f);
                                    return true;
                                });
                                return savedFilm is not null;
                            },
                            () => savedFilm is null || CataloguePlayableSearchDocument.ShouldExcludeFromSearch(savedFilm)
                                ? null
                                : CataloguePlayableSearchDocument.For(savedFilm),
                            () => SyncYouTubeIdIfNeeded(film),
                            cancellationToken);

                        Tally(outcome, ref identical, ref missingTruth, ref discrepancies, ref applied,
                            ref searchUploaded, ref failedWrites);
                        if (request.Limit > 0 && discrepancies >= request.Limit)
                        {
                            return Finish(scanned, identical, missingTruth, discrepancies, applied, searchUploaded,
                                failedWrites);
                        }
                    }

                    break;

                case PlayableKind.TvShowEpisode:
                    await foreach (var episode in tvShowEpisodeRepository.GetAll().WithCancellation(cancellationToken))
                    {
                        if (!Include(episode.Id, idFilter))
                        {
                            continue;
                        }

                        scanned++;
                        LogProgress(scanned, progressEvery);
                        var outcome = await ProcessOne(
                            request,
                            publicIndex,
                            changeLog,
                            PlayableKind.TvShowEpisode,
                            episode.Id,
                            episode.TvShowId,
                            episode.Services,
                            services =>
                            {
                                episode.Services = services;
                                EpisodeServicePresence.SyncCatalogueYouTubeId(episode);
                                return SaveTv(episode);
                            },
                            () => CataloguePlayableSearchDocument.ShouldExcludeFromSearch(episode)
                                ? null
                                : CataloguePlayableSearchDocument.For(episode),
                            () => SyncYouTubeIdIfNeeded(episode),
                            cancellationToken);

                        Tally(outcome, ref identical, ref missingTruth, ref discrepancies, ref applied,
                            ref searchUploaded, ref failedWrites);
                        if (request.Limit > 0 && discrepancies >= request.Limit)
                        {
                            return Finish(scanned, identical, missingTruth, discrepancies, applied, searchUploaded,
                                failedWrites);
                        }
                    }

                    break;

                case PlayableKind.NewsReport:
                    await foreach (var report in newsReportRepository.GetAll().WithCancellation(cancellationToken))
                    {
                        if (!Include(report.Id, idFilter))
                        {
                            continue;
                        }

                        scanned++;
                        LogProgress(scanned, progressEvery);
                        var outcome = await ProcessOne(
                            request,
                            publicIndex,
                            changeLog,
                            PlayableKind.NewsReport,
                            report.Id,
                            report.NewsOrganisationId,
                            report.Services,
                            services =>
                            {
                                report.Services = services;
                                EpisodeServicePresence.SyncCatalogueYouTubeId(report);
                                return SaveNews(report);
                            },
                            () => CataloguePlayableSearchDocument.ShouldExcludeFromSearch(report)
                                ? null
                                : CataloguePlayableSearchDocument.For(report),
                            () => SyncYouTubeIdIfNeeded(report),
                            cancellationToken);

                        Tally(outcome, ref identical, ref missingTruth, ref discrepancies, ref applied,
                            ref searchUploaded, ref failedWrites);
                        if (request.Limit > 0 && discrepancies >= request.Limit)
                        {
                            return Finish(scanned, identical, missingTruth, discrepancies, applied, searchUploaded,
                                failedWrites);
                        }
                    }

                    break;
            }
        }

        return Finish(scanned, identical, missingTruth, discrepancies, applied, searchUploaded, failedWrites);
    }

    private async Task<ProcessOutcome> ProcessOne(
        CataloguePlayableServicesBackfillRequest request,
        IPublicDatabaseUrlIndex publicIndex,
        IChangeLogWriter changeLog,
        PlayableKind kind,
        Guid id,
        Guid? parentId,
        Dictionary<string, ServiceLink>? liveServices,
        Func<Dictionary<string, ServiceLink>?, Task<bool>> saveAsync,
        Func<RedditPodcastPoster.Search.Models.EpisodeSearchRecord?> buildSearchDocument,
        Func<bool> syncYouTubeIdIfNeeded,
        CancellationToken cancellationToken)
    {
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
        var youtubeIdChanged = syncYouTubeIdIfNeeded();
        var hasTruth = !request.YouTubeIdOnly && (episodeBackup is not null || publicHit is not null);
        if (!hasTruth && !request.YouTubeIdOnly && !youtubeIdChanged)
        {
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
                MissingTruth = true
            }, cancellationToken);
            return ProcessOutcome.MissingTruth;
        }

        var truth = hasTruth
            ? TruthServicesMerger.Merge(episodeBackup, publicHit?.Urls)
            : new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
        var carry = PlayableServicesDiscrepancyCarry.Apply(liveServices, truth);
        if (!carry.HasChanges && !youtubeIdChanged)
        {
            return ProcessOutcome.Identical;
        }

        var changes = carry.HasChanges
            ? carry.Changes.ToList()
            : [];
        if (youtubeIdChanged)
        {
            changes.Add(new ServiceFieldChange(ServiceKeys.YouTube, "youTubeId", null, "synced"));
        }

        if (!string.IsNullOrWhiteSpace(request.SnapshotDir))
        {
            var beforePath = Path.Combine(
                request.SnapshotDir,
                $"{PlayableKindParser.ToWire(kind)}-{id:D}-services-before.json");
            await File.WriteAllTextAsync(
                beforePath,
                JsonSerializer.Serialize(liveServices, BackupDocumentJsonOptions.Instance),
                cancellationToken);
        }

        var publicPath = publicHit?.FilePath ?? string.Empty;
        var applied = false;
        var searchOk = false;
        if (request.Apply)
        {
            try
            {
                // When only youTubeId needs persist, re-save live services so SyncCatalogueYouTubeId runs in saveAsync.
                var servicesToSave = carry.HasChanges ? carry.Services : liveServices;
                var saved = await saveAsync(servicesToSave);
                if (!saved)
                {
                    logger.LogError(
                        "Failed to save {Kind} {Id} after carrying services from backup={BackupPath} public={PublicPath}.",
                        kind, id, backupPath, publicPath);
                    await WriteChange(changeLog, kind, id, parentId, backupPath, publicPath, changes,
                        applied: false, cancellationToken);
                    return ProcessOutcome.FailedWrite;
                }

                applied = true;
                var document = buildSearchDocument();
                if (document is not null)
                {
                    var swap = searchSwapFactory();
                    searchOk = await swap.UploadAsync([document], cancellationToken);
                    if (!searchOk)
                    {
                        logger.LogError(
                            "Cosmos save succeeded for {Kind} {Id} but search upload failed.",
                            kind, id);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply services carry for {Kind} {Id}.", kind, id);
                await WriteChange(changeLog, kind, id, parentId, backupPath, publicPath, changes,
                    applied: false, cancellationToken);
                return ProcessOutcome.FailedWrite;
            }
        }

        await WriteChange(changeLog, kind, id, parentId, backupPath, publicPath, changes, applied,
            cancellationToken);
        return applied
            ? (searchOk ? ProcessOutcome.AppliedWithSearch : ProcessOutcome.Applied)
            : ProcessOutcome.DiscrepancyDryRun;
    }

    /// <summary>
    /// Sync compact youTubeId from services.youtube.url; returns true when the stored value changed.
    /// </summary>
    private static bool SyncYouTubeIdIfNeeded(Film film)
    {
        var before = film.YouTubeId;
        EpisodeServicePresence.SyncCatalogueYouTubeId(film);
        return !string.Equals(before, film.YouTubeId, StringComparison.Ordinal);
    }

    private static bool SyncYouTubeIdIfNeeded(TvShowEpisode episode)
    {
        var before = episode.YouTubeId;
        EpisodeServicePresence.SyncCatalogueYouTubeId(episode);
        return !string.Equals(before, episode.YouTubeId, StringComparison.Ordinal);
    }

    private static bool SyncYouTubeIdIfNeeded(NewsReport report)
    {
        var before = report.YouTubeId;
        EpisodeServicePresence.SyncCatalogueYouTubeId(report);
        return !string.Equals(before, report.YouTubeId, StringComparison.Ordinal);
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
            Utc = timeProvider.GetUtcNow()
        }, cancellationToken);

    private async Task<bool> SaveTv(TvShowEpisode episode)
    {
        await tvShowEpisodeRepository.Save(episode);
        return true;
    }

    private async Task<bool> SaveNews(NewsReport report)
    {
        await newsReportRepository.Save(report);
        return true;
    }

    private void LogProgress(int scanned, int progressEvery)
    {
        if (scanned % progressEvery == 0)
        {
            logger.LogInformation("CataloguePlayableServicesBackfill scanned {Scanned} playables…", scanned);
        }
    }

    private static bool Include(Guid id, HashSet<Guid>? filter) =>
        filter is null || filter.Contains(id);

    private static void Tally(
        ProcessOutcome outcome,
        ref int identical,
        ref int missingTruth,
        ref int discrepancies,
        ref int applied,
        ref int searchUploaded,
        ref int failedWrites)
    {
        switch (outcome)
        {
            case ProcessOutcome.Identical:
                identical++;
                break;
            case ProcessOutcome.MissingTruth:
                missingTruth++;
                break;
            case ProcessOutcome.DiscrepancyDryRun:
                discrepancies++;
                break;
            case ProcessOutcome.Applied:
                discrepancies++;
                applied++;
                break;
            case ProcessOutcome.AppliedWithSearch:
                discrepancies++;
                applied++;
                searchUploaded++;
                break;
            case ProcessOutcome.FailedWrite:
                discrepancies++;
                failedWrites++;
                break;
        }
    }

    private CataloguePlayableServicesBackfillRunResult Finish(
        int scanned,
        int identical,
        int missingTruth,
        int discrepancies,
        int applied,
        int searchUploaded,
        int failedWrites)
    {
        logger.LogInformation(
            "CataloguePlayableServicesBackfill done. Scanned={Scanned} Identical={Identical} MissingTruth={MissingTruth} Discrepancies={Discrepancies} Applied={Applied} SearchUploaded={SearchUploaded} FailedWrites={FailedWrites}",
            scanned, identical, missingTruth, discrepancies, applied, searchUploaded, failedWrites);
        var exitCode = failedWrites > 0 ? ExitFailed : ExitOk;
        return new CataloguePlayableServicesBackfillRunResult(
            exitCode, scanned, identical, missingTruth, discrepancies, applied, searchUploaded, failedWrites);
    }

    private static CataloguePlayableServicesBackfillRunResult Empty(int exitCode) =>
        new(exitCode, 0, 0, 0, 0, 0, 0, 0);

    private enum ProcessOutcome
    {
        Identical,
        MissingTruth,
        DiscrepancyDryRun,
        Applied,
        AppliedWithSearch,
        FailedWrite
    }
}
