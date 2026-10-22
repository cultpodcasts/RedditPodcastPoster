using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace CatalogueMigrate;

/// <summary>One logged plan line. Dry-run and apply produce the same lines for the same corpus.</summary>
public sealed record CatalogueMigratePlanLine(
    Guid PodcastId,
    string Kind,
    Guid? ParentId,
    int PlayableCount,
    int RemovedPlayableCount,
    int SearchDocumentCount,
    bool RequiresAllowlist,
    bool OnAllowlist,
    bool Accepted,
    string? BlockReason);

public sealed record CatalogueMigrateRunResult(
    int ExitCode,
    int PlannedCount,
    int SearchDocumentCount,
    IReadOnlyList<CatalogueMigratePlanLine>? Plan = null,
    int AppliedCount = 0,
    int ConflictCount = 0,
    IReadOnlyList<string>? EvidenceFiles = null)
{
    public IReadOnlyList<CatalogueMigratePlanLine> PlanLines => Plan ?? [];
    public IReadOnlyList<string> Evidence => EvidenceFiles ?? [];
}

/// <summary>
/// Corpus migrate (S-006, S-007, S-008). Dry-run by default. <c>--apply</c> writes only with the
/// exact consent phrase (<see cref="CatalogueMigrateConsent"/>); otherwise exit 2.
/// Exit codes: 0 ok, 1 bad arguments, 2 apply refused, 3 a podcast failed (run stopped; re-run resumes).
/// </summary>
public class CatalogueMigrateProcessor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    Func<CatalogueMigrateApplier> applierFactory,
    ICatalogueMigrateDestinationReader destinationReader,
    CatalogueMigrateRollbackPreviewer rollbackPreviewer,
    ICatalogueMigrateEvidenceStore evidenceStore,
    TimeProvider timeProvider,
    ILogger<CatalogueMigrateProcessor> logger)
{
    public const int ExitOk = 0;
    public const int ExitBadArguments = 1;
    public const int ExitApplyRefused = 2;
    public const int ExitFailed = 3;

    public async Task<CatalogueMigrateRunResult> Run(
        CatalogueMigrateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Rollback is not null)
        {
            return await RunRollback(request, cancellationToken);
        }

        if (!IsSupportedKind(request.Kind))
        {
            logger.LogError(
                "Catalogue migrate --kind must be NewsReport, Film, or TvShowEpisode. Got {Kind}.",
                request.Kind);
            return new CatalogueMigrateRunResult(ExitBadArguments, 0, 0);
        }

        if (request.Apply && !CatalogueMigrateConsent.IsApplyConsented(request))
        {
            logger.LogError(
                "Catalogue migrate --apply is refused (exit 2): --consent is missing or is not the exact phrase for --kind {Kind}. See docs/catalogue-run/gate-5.md. Agents must never supply consent.",
                request.Kind);
            return new CatalogueMigrateRunResult(ExitApplyRefused, 0, 0);
        }

        if (request.Kind == SubmitClassification.Film && request.PodcastId is null)
        {
            logger.LogError(
                "Catalogue migrate --kind Film requires --podcast-id, because stored identify cannot flag Film.");
            return new CatalogueMigrateRunResult(ExitBadArguments, 0, 0);
        }

        if (request.BatchSize is < 1 or > CatalogueMigrateRequest.MaxBatchSize || request.Limit is < 1)
        {
            logger.LogError(
                "Catalogue migrate --batch-size must be 1-{Max} and --limit at least 1.",
                CatalogueMigrateRequest.MaxBatchSize);
            return new CatalogueMigrateRunResult(ExitBadArguments, 0, 0);
        }

        var apply = request.Apply;
        logger.LogInformation(
            apply
                ? "Catalogue migrate APPLY kind={Kind} — writes Cosmos and swaps Search. Order is News then Film then TV."
                : "Catalogue migrate dry-run kind={Kind} — no Cosmos writes and no search upload. Order is News then Film then TV.",
            request.Kind);

        var runId = timeProvider.GetUtcNow().ToString("yyyyMMdd'T'HHmmss'Z'");
        var plan = new List<CatalogueMigratePlanLine>();
        var evidenceFiles = new List<string>();
        var searchDocumentCount = 0;
        var appliedCount = 0;
        var conflictCount = 0;
        var eligibleCount = 0;
        var batchNumber = 0;
        var inBatch = 0;
        ICatalogueMigrateEvidenceBatch? batch = null;
        var applier = apply ? applierFactory() : null;
        var exitCode = ExitOk;

        try
        {
            // Buffer the podcast list first: apply deletes source rows while the run walks the corpus.
            var podcasts = await EnumeratePodcasts(request.PodcastId).ToListAsync(cancellationToken);
            foreach (var listed in podcasts)
            {
                // Apply plans from a fresh read, not the list buffered at the start: a long run can cross an Hourly pass
                // (or a curator edit), and the stale copy would be the destination's settings, the evidence snapshot a
                // rollback restores, and the baseline the pre-delete re-check compares against. A dry run writes
                // nothing, so it keeps the buffered copy and costs no extra reads.
                var podcast = apply && request.PodcastId is null
                    ? await podcastRepository.GetPodcast(listed.Id)
                    : listed;
                if (podcast is null)
                {
                    logger.LogWarning(
                        "Catalogue migrate podcast {PodcastId} no longer exists (deleted or moved since the run started). Skipped.",
                        listed.Id);
                    continue;
                }

                var episodes = await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync(cancellationToken);
                var unit = CatalogueMigratePlanner.PlanUnit(podcast, episodes, request.Kind, request.PodcastId is not null);
                if (unit is null)
                {
                    continue;
                }

                if (!unit.Plan.Accepted)
                {
                    logger.LogWarning(
                        "Catalogue migrate rejected {PodcastId} {Kind}: {Reason}",
                        podcast.Id,
                        request.Kind,
                        unit.Plan.RejectReason);
                    continue;
                }

                unit = await BlockDuplicateParent(unit);
                if (apply)
                {
                    unit = BlockWhileIndexerMayBeRunning(unit);
                }

                if (unit.ApplyEligible && request.Limit is { } limit && eligibleCount >= limit)
                {
                    logger.LogInformation("Catalogue migrate --limit {Limit} reached. Re-run to continue.", limit);
                    break;
                }

                var line = LogPlan(unit);
                plan.Add(line);
                searchDocumentCount += line.SearchDocumentCount;
                if (!unit.ApplyEligible)
                {
                    continue;
                }

                eligibleCount++;
                if (applier is null)
                {
                    continue;
                }

                if (batch is null || inBatch >= request.BatchSize)
                {
                    if (batch is not null)
                    {
                        await batch.DisposeAsync();
                    }

                    batchNumber++;
                    inBatch = 0;
                    batch = evidenceStore.OpenBatch(request.EvidenceDirectory, runId, request.Kind, batchNumber);
                    evidenceFiles.Add(batch.Name);
                    logger.LogInformation("Catalogue migrate evidence batch {Batch}: {Path}", batchNumber, batch.Name);
                }

                inBatch++;
                var outcome = await applier.Apply(unit, batch, runId, cancellationToken);
                if (outcome == CatalogueMigrateApplyOutcome.Completed)
                {
                    appliedCount++;
                }
                else if (outcome == CatalogueMigrateApplyOutcome.Conflict)
                {
                    conflictCount++;
                }
                else
                {
                    logger.LogError(
                        "Catalogue migrate stopped after a failure on {PodcastId}. Evidence: {Path}. Re-run resumes; or roll back this batch with --rollback.",
                        podcast.Id,
                        batch.Name);
                    exitCode = ExitFailed;
                    break;
                }
            }
        }
        finally
        {
            if (batch is not null)
            {
                await batch.DisposeAsync();
            }
        }

        logger.LogInformation(
            "Catalogue migrate {Mode} complete. Planned={Count} ApplyEligible={Eligible} SearchDocuments={SearchDocumentCount} Applied={Applied} Conflicts={Conflicts}",
            apply ? "apply" : "dry-run",
            plan.Count,
            eligibleCount,
            searchDocumentCount,
            appliedCount,
            conflictCount);
        return new CatalogueMigrateRunResult(
            exitCode,
            plan.Count,
            searchDocumentCount,
            plan,
            appliedCount,
            conflictCount,
            evidenceFiles);
    }

    private async Task<CatalogueMigrateRunResult> RunRollback(
        CatalogueMigrateRequest request,
        CancellationToken cancellationToken)
    {
        var path = request.Rollback!;
        if (request.Apply && !CatalogueMigrateConsent.IsRollbackConsented(request))
        {
            logger.LogError(
                "Catalogue migrate --rollback --apply is refused (exit 2): --consent is missing or is not the exact rollback phrase for {File}. See docs/catalogue-run/gate-5.md.",
                Path.GetFileName(path));
            return new CatalogueMigrateRunResult(ExitApplyRefused, 0, 0);
        }

        if (!evidenceStore.Exists(path))
        {
            logger.LogError("Catalogue migrate --rollback evidence file not found: {Path}", path);
            return new CatalogueMigrateRunResult(ExitBadArguments, 0, 0);
        }

        var begins = new List<CatalogueMigrateEvidenceRecord>();
        var conflicts = new HashSet<Guid>();
        await foreach (var record in evidenceStore.Read(path).WithCancellation(cancellationToken))
        {
            if (record.Event == CatalogueMigrateEvidenceEvent.Begin)
            {
                begins.Add(record);
            }
            else if (record.Event == CatalogueMigrateEvidenceEvent.Conflict)
            {
                conflicts.Add(record.PodcastId);
            }
        }

        var targets = begins.Where(begin => !conflicts.Contains(begin.PodcastId)).ToList();
        // A preview only reads the destination, so it never builds the applier (and with it the Search client).
        var applier = request.Apply ? applierFactory() : null;
        var runId = timeProvider.GetUtcNow().ToString("yyyyMMdd'T'HHmmss'Z'");
        var restored = 0;
        var exitCode = ExitOk;
        var evidenceFiles = new List<string>();
        ICatalogueMigrateEvidenceBatch batch = request.Apply
            ? evidenceStore.OpenBatch(request.EvidenceDirectory, runId, "rollback", 1)
            : NullEvidenceBatch.Instance;
        if (request.Apply)
        {
            evidenceFiles.Add(batch.Name);
        }

        await using (batch)
        {
            foreach (var begin in targets)
            {
                var ok = applier is not null
                    ? await applier.Rollback(begin, batch, runId, request.Apply, cancellationToken)
                    : !(await rollbackPreviewer.Preview(begin, apply: false)).Refused;
                if (ok)
                {
                    restored++;
                }
                else
                {
                    exitCode = ExitFailed;
                }
            }
        }

        logger.LogInformation(
            "Catalogue migrate rollback {Mode} complete. Podcasts={Count} Restored={Restored} Skipped(conflict)={Conflicts}",
            request.Apply ? "apply" : "dry-run",
            targets.Count,
            restored,
            conflicts.Count);
        return new CatalogueMigrateRunResult(exitCode, targets.Count, 0, AppliedCount: request.Apply ? restored : 0, EvidenceFiles: evidenceFiles);
    }

    /// <summary>
    /// Blocks a News / TV move whose destination name or fileKey already belongs to another parent of that kind
    /// (a channel alias or canonical TV name, or an outlet already moved by change-kind). Apply would otherwise
    /// create a second parent with the same name and fileKey instead of attaching to the existing one.
    /// </summary>
    private async Task<CatalogueMigrateUnit> BlockDuplicateParent(CatalogueMigrateUnit unit)
    {
        if (!unit.ApplyEligible || CatalogueMigratePlanner.DestinationParent(unit.Podcast, unit.Kind) is not { } parent)
        {
            return unit;
        }

        var existing = await destinationReader.FindOtherParentWithName(unit.Kind, parent.Name, parent.FileKey, parent.Id);
        if (existing is null)
        {
            return unit;
        }

        logger.LogWarning(
            "Catalogue migrate {PodcastId} {Kind}: parent name '{ParentName}' already belongs to {ExistingParentId}. Blocked.",
            unit.Podcast.Id,
            unit.Kind,
            parent.Name,
            existing);
        return unit with { ApplyBlockReason = CatalogueMigratePlanner.ParentNameExists };
    }

    /// <summary>
    /// Apply only: an auto-indexed podcast is skipped (not failed) while the hourly indexer may be running, so the
    /// run carries on with the other podcasts and a re-run in the safe window moves it.
    /// </summary>
    private CatalogueMigrateUnit BlockWhileIndexerMayBeRunning(CatalogueMigrateUnit unit)
    {
        if (!unit.ApplyEligible || !CatalogueMigratePlanner.IsAutoIndexed(unit.Podcast))
        {
            return unit;
        }

        var now = timeProvider.GetUtcNow();
        if (!CatalogueMigrateIndexerWindow.IndexerMayBeRunning(now))
        {
            return unit;
        }

        logger.LogWarning(
            "Catalogue migrate {PodcastId} {Kind}: auto-indexed and the hourly indexer may be running (minute {Minute}). " +
            "Skipped; re-run between :{From} and :{Until}.",
            unit.Podcast.Id,
            unit.Kind,
            now.Minute,
            CatalogueMigrateIndexerWindow.SafeFromMinute,
            CatalogueMigrateIndexerWindow.SafeUntilMinute - 1);
        return unit with { ApplyBlockReason = CatalogueMigratePlanner.IndexerMayBeRunning };
    }

    private CatalogueMigratePlanLine LogPlan(CatalogueMigrateUnit unit)
    {
        var line = new CatalogueMigratePlanLine(
            unit.Podcast.Id,
            unit.Kind,
            unit.Plan.DestParentId,
            unit.MoveEpisodes.Count,
            unit.RemovedEpisodeCount,
            unit.SearchEpisodes.Count,
            unit.Plan.RequiresAllowlist,
            unit.OnAllowlist,
            unit.ApplyEligible,
            unit.ApplyBlockReason);
        logger.LogInformation(
            "Catalogue migrate plan {PodcastId} {Kind} parent={ParentId} playables={PlayableCount} removed={RemovedCount} searchDocuments={SearchDocumentCount} allowlist={RequiresAllowlist} onAllowlist={OnAllowlist} apply={ApplyEligible} block={BlockReason}",
            line.PodcastId,
            line.Kind,
            line.ParentId,
            line.PlayableCount,
            line.RemovedPlayableCount,
            line.SearchDocumentCount,
            line.RequiresAllowlist,
            line.OnAllowlist,
            line.Accepted,
            line.BlockReason);
        return line;
    }

    private static bool IsSupportedKind(string kind) =>
        kind is SubmitClassification.NewsReport
            or SubmitClassification.Film
            or SubmitClassification.TvShowEpisode;

    private IAsyncEnumerable<Podcast> EnumeratePodcasts(Guid? podcastId)
    {
        if (podcastId is not { } id)
        {
            return podcastRepository.GetAll();
        }

        return EnumerateOne(id);
    }

    private async IAsyncEnumerable<Podcast> EnumerateOne(Guid id)
    {
        var podcast = await podcastRepository.GetPodcast(id);
        if (podcast is not null)
        {
            yield return podcast;
        }
        else
        {
            logger.LogWarning("Catalogue migrate podcast {PodcastId} not found (already migrated?).", id);
        }
    }

    private sealed class NullEvidenceBatch : ICatalogueMigrateEvidenceBatch
    {
        public static readonly NullEvidenceBatch Instance = new();
        public string Name => "(dry-run)";
        public Task Write(CatalogueMigrateEvidenceRecord record) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
