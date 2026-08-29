using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;

namespace EpisodeServiceBackfill;

/// <summary>
/// Dry-run (default) or apply backfill of <c>services</c> + <c>ids</c> onto episode documents.
/// Selection and patch payloads use raw JSON. Apply is a surgical Cosmos patch — not a full upsert.
/// Query iteration is the caller's job; this type parallelises CPU work and PatchItemAsync only.
/// </summary>
public class EpisodeServiceBackfillProcessor(
    IBackfillEpisodeRepository episodeRepository,
    IEpisodeCatalogPatchSource catalogPatchSource,
    ILogger<EpisodeServiceBackfillProcessor> logger)
{
    public const int DefaultDegreeOfParallelism = 8;

    public async Task<EpisodeServiceBackfillReport> RunAsync(
        IReadOnlyList<string> rawDocuments,
        bool apply,
        int maxDegreeOfParallelism = DefaultDegreeOfParallelism,
        EpisodeServiceBackfillSpotCheckSampler? sampler = null,
        EpisodeServiceBackfillPatchLogWriter? patchLog = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawDocuments);

        var candidates = 0;
        var saved = 0;
        var missing = 0;
        var mismatches = 0;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism),
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(rawDocuments, parallelOptions, async (json, ct) =>
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            if (!catalogPatchSource.TryCreate(json, out var patch) || patch is null)
            {
                return;
            }

            if (!EpisodeServiceCatalogPatchIdentity.Matches(json, patch, out var reason))
            {
                Interlocked.Increment(ref mismatches);
                logger.LogError(
                    "Episode service backfill: identity mismatch ({Reason}). Patch episode {EpisodeId} podcast {PodcastId} was not written.",
                    reason,
                    patch.EpisodeId,
                    patch.PodcastId);
                return;
            }

            Interlocked.Increment(ref candidates);
            if (!apply)
            {
                sampler?.Offer(patch);
                patchLog?.Write(patch, applied: false);
                return;
            }

            ct.ThrowIfCancellationRequested();
            var written = await episodeRepository.PatchServicesAndIds(
                patch.PodcastId,
                patch.EpisodeId,
                patch.Services,
                patch.Ids);
            if (!written)
            {
                Interlocked.Increment(ref missing);
                patchLog?.Write(patch, applied: false);
                logger.LogWarning(
                    "Episode service backfill: episode {EpisodeId} podcast {PodcastId} was not found; skip.",
                    patch.EpisodeId,
                    patch.PodcastId);
                return;
            }

            Interlocked.Increment(ref saved);
            sampler?.Offer(patch);
            patchLog?.Write(patch, applied: true);
        });

        logger.LogInformation(
            "Episode service backfill: {CandidateCount} document(s) need services/ids. Apply={Apply}. Mismatches={Mismatches}.",
            candidates,
            apply,
            mismatches);

        if (!apply)
        {
            return new EpisodeServiceBackfillReport(candidates, 0, 0, 0, Applied: false, mismatches);
        }

        logger.LogInformation(
            "Episode service backfill apply complete. Saved={Saved}, Missing={Missing}, Mismatches={Mismatches}.",
            saved,
            missing,
            mismatches);
        return new EpisodeServiceBackfillReport(candidates, saved, 0, missing, Applied: true, mismatches);
    }

    public async Task<bool> ApplyPatchAsync(
        string json,
        EpisodeServiceCatalogPatch patch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(patch);
        cancellationToken.ThrowIfCancellationRequested();
        if (!EpisodeServiceCatalogPatchIdentity.Matches(json, patch, out var reason))
        {
            logger.LogError(
                "Episode service backfill: refusing patch ({Reason}). Patch episode {EpisodeId} podcast {PodcastId} was not written.",
                reason,
                patch.EpisodeId,
                patch.PodcastId);
            return false;
        }

        return await episodeRepository.PatchServicesAndIds(
            patch.PodcastId,
            patch.EpisodeId,
            patch.Services,
            patch.Ids);
    }
}

public readonly record struct EpisodeServiceBackfillReport(
    int Candidates,
    int Saved,
    int Unchanged,
    int Missing,
    bool Applied,
    int Mismatches = 0);
