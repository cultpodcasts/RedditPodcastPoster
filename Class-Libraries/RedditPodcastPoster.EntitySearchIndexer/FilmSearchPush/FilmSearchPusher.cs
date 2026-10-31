using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

/// <summary>
/// Rules:
/// <list type="bullet">
/// <item>Incremental run: films with Cosmos <c>_ts &gt;=</c> the saved watermark. The boundary second is re-pushed
/// (idempotent) so a film saved later in that second is not skipped, the same <c>&gt;=</c> the pull indexers use.</item>
/// <item>Full run: every film, plus a delete for each film document in the index whose film no longer exists in
/// Cosmos (a hard delete leaves no <c>_ts</c> to find). A full run happens when asked for, when there is no
/// watermark yet, or when the index holds no film documents (for example straight after a teardown).</item>
/// <item>A removed film's document is deleted; any other film is merge-or-uploaded with
/// <see cref="CataloguePlayableSearchDocument.For(Film)"/>, the same document the curator edit pushes.</item>
/// <item>The watermark advances to the highest <c>_ts</c> read only after every push and delete succeeded, so a
/// failed incremental run is retried next time. A failed full run clears the watermark, so the next run is full
/// again. A dry run writes nothing.</item>
/// </list>
/// </summary>
public sealed class FilmSearchPusher(
    IFilmRepository filmRepository,
    IFilmSearchPushWatermarkStore watermarkStore,
    IFilmSearchIndexGateway searchIndex,
    TimeProvider timeProvider,
    ILogger<FilmSearchPusher> logger) : IFilmSearchPusher
{
    public async Task<FilmSearchPushResult> Push(FilmSearchPushRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.BatchSize is < 1 or > FilmSearchPushRequest.MaxBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(request),
                $"Batch size must be between 1 and {FilmSearchPushRequest.MaxBatchSize}.");
        }

        if (request.FilmId is { } singleFilmId)
        {
            if (request.Full)
            {
                throw new ArgumentException("A single-film push cannot also be a full push.", nameof(request));
            }

            return await PushOne(singleFilmId, request.DryRun, cancellationToken);
        }

        var saved = await watermarkStore.GetAsync(cancellationToken);
        var previous = saved?.HighWaterMark;
        var full = await DecideFull(request, previous, cancellationToken);

        List<Film> films;
        if (full)
        {
            films = await filmRepository.GetAll().ToListAsync(cancellationToken);
        }
        else
        {
            var since = previous!.Value;
            films = await filmRepository.GetAllBy(film => film.Timestamp >= since).ToListAsync(cancellationToken);
        }

        logger.LogInformation(
            "Film search push: {Mode} run read {FilmCount} film(s) from Cosmos (watermark {Watermark}).",
            full ? "full" : "incremental", films.Count, previous?.ToString() ?? "none");

        var toPush = new List<EpisodeSearchRecord>();
        var toDelete = new List<string>();
        var failed = 0;
        foreach (var film in films)
        {
            if (CataloguePlayableSearchDocument.ShouldExcludeFromSearch(film))
            {
                toDelete.Add(film.Id.ToString());
                continue;
            }

            try
            {
                toPush.Add(CataloguePlayableSearchDocument.For(film));
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(ex, "Film search push: could not build the search document for film '{FilmId}'.", film.Id);
            }
        }

        if (full)
        {
            var existing = films.Select(film => film.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var orphans = new List<string>();
            foreach (var id in (await searchIndex.GetFilmDocumentIds(cancellationToken)).Where(id => !existing.Contains(id)))
            {
                // Re-read before deleting: a film created (for example by Move to film) after the films were read
                // already has its document, and must not be removed as an orphan.
                if (Guid.TryParse(id, out var filmId) && await filmRepository.GetFilm(filmId) is null)
                {
                    orphans.Add(id);
                }
            }

            if (orphans.Count > 0)
            {
                logger.LogInformation(
                    "Film search push: {OrphanCount} film document(s) in the index have no film in Cosmos and will be deleted.",
                    orphans.Count);
                toDelete.AddRange(orphans);
            }
        }

        var highest = films.Count == 0 ? (long?)null : films.Max(film => film.Timestamp);
        var next = Max(previous, highest);
        if (request.DryRun)
        {
            logger.LogInformation(
                "Film search push DRY RUN: would push {PushCount} and delete {DeleteCount} document(s); watermark would move {From} -> {To}. Nothing written.",
                toPush.Count, toDelete.Count, previous?.ToString() ?? "none", next?.ToString() ?? "none");
            return new FilmSearchPushResult(failed == 0, full, true, previous, previous, films.Count, toPush.Count,
                toDelete.Count, failed, false);
        }

        try
        {
            foreach (var batch in toPush.Chunk(request.BatchSize))
            {
                failed += (await searchIndex.MergeOrUpload(batch, cancellationToken)).Count;
            }

            foreach (var batch in toDelete.Chunk(request.BatchSize))
            {
                failed += (await searchIndex.Delete(batch, cancellationToken)).Count;
            }
        }
        catch (Exception ex) when (full && saved?.HighWaterMark is not null)
        {
            // A whole request failing (quota, network, cancellation) throws instead of returning failed keys. Earlier
            // batches may already be in the index, so the empty-index check would no longer promote the next run to
            // full: clear the watermark first, then let the failure surface.
            logger.LogError(ex,
                "Film search push: a full run failed part-way; clearing the watermark so the next run is full again.");
            // Keep the push failure as the one that surfaces; a store failure is only logged.
            await TryClearWatermarkAfterFailedFullRun(saved);

            throw;
        }

        if (failed > 0)
        {
            if (full && saved?.HighWaterMark is not null)
            {
                // A partly-failed full run (for example straight after a teardown) leaves some film documents in the
                // index, so the empty-index check would no longer catch it. Clearing the watermark makes the next
                // run full again instead of incremental from a watermark older films sit below.
                // The clear is guarded like the throw path: a store failure is logged ("run with --full next") and
                // the failed-documents outcome is still returned, rather than the store exception surfacing instead.
                logger.LogError(
                    "Film search push: {FailedCount} document(s) failed in a full run; clearing the watermark so the next run is full again.",
                    failed);
                var cleared = await TryClearWatermarkAfterFailedFullRun(saved);
                return new FilmSearchPushResult(false, full, false, previous, cleared ? null : previous, films.Count,
                    toPush.Count, toDelete.Count, failed, cleared);
            }

            logger.LogError(
                "Film search push: {FailedCount} document(s) failed; watermark kept at {Watermark} so the next run retries them.",
                failed, previous?.ToString() ?? "none");
            return new FilmSearchPushResult(false, full, false, previous, previous, films.Count, toPush.Count,
                toDelete.Count, failed, false);
        }

        var watermark = saved ?? new FilmSearchPushWatermark();
        watermark.HighWaterMark = next;
        watermark.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;
        watermark.LastRunPushed = toPush.Count;
        watermark.LastRunDeleted = toDelete.Count;
        await watermarkStore.SaveAsync(watermark, cancellationToken);
        logger.LogInformation(
            "Film search push: pushed {PushCount}, deleted {DeleteCount}; watermark {From} -> {To}.",
            toPush.Count, toDelete.Count, previous?.ToString() ?? "none", next?.ToString() ?? "none");
        return new FilmSearchPushResult(true, full, false, previous, next, films.Count, toPush.Count, toDelete.Count,
            0, true);
    }

    /// <summary>
    /// One film by id: merge-or-upload its document, or delete it when the film is removed. The watermark is neither
    /// read nor saved, so the scheduled incremental run is unaffected. A missing film fails without writing.
    /// </summary>
    private async Task<FilmSearchPushResult> PushOne(Guid filmId, bool dryRun, CancellationToken cancellationToken)
    {
        var film = await filmRepository.GetFilm(filmId);
        if (film is null)
        {
            logger.LogError("Film search push: film '{FilmId}' was not found in Cosmos; nothing pushed.", filmId);
            return new FilmSearchPushResult(false, false, dryRun, null, null, 0, 0, 0, 1, false);
        }

        var delete = CataloguePlayableSearchDocument.ShouldExcludeFromSearch(film);
        var document = delete ? null : CataloguePlayableSearchDocument.For(film);
        if (dryRun)
        {
            logger.LogInformation("Film search push DRY RUN: would {Action} film '{FilmId}'. Nothing written.",
                delete ? "delete" : "push", filmId);
            return new FilmSearchPushResult(true, false, true, null, null, 1, delete ? 0 : 1, delete ? 1 : 0, 0, false);
        }

        var failed = delete
            ? (await searchIndex.Delete([film.Id.ToString()], cancellationToken)).Count
            : (await searchIndex.MergeOrUpload([document!], cancellationToken)).Count;
        logger.LogInformation("Film search push: {Action} film '{FilmId}' {Outcome}; watermark unchanged.",
            delete ? "deleted" : "pushed", filmId, failed == 0 ? "ok" : "FAILED");
        return new FilmSearchPushResult(failed == 0, false, false, null, null, 1, delete ? 0 : 1, delete ? 1 : 0,
            failed, false);
    }

    /// <summary>
    /// Clears the watermark after a full run failed (thrown or with failed documents). A store failure is logged, not
    /// thrown, so the run's own failure is what the caller sees.
    /// </summary>
    /// <returns><c>true</c> if the watermark was cleared.</returns>
    private async Task<bool> TryClearWatermarkAfterFailedFullRun(FilmSearchPushWatermark saved)
    {
        try
        {
            await ClearWatermark(saved);
            return true;
        }
        catch (Exception clearEx)
        {
            logger.LogError(clearEx,
                "Film search push: the watermark could not be cleared after the failed full run; run with --full next.");
            return false;
        }
    }

    /// <summary>Not cancellable: it runs on the failure path, where the run's token may already be cancelled.</summary>
    private async Task ClearWatermark(FilmSearchPushWatermark saved)
    {
        saved.HighWaterMark = null;
        saved.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;
        await watermarkStore.SaveAsync(saved, CancellationToken.None);
    }

    private async Task<bool> DecideFull(FilmSearchPushRequest request, long? previous, CancellationToken cancellationToken)
    {
        if (request.Full)
        {
            return true;
        }

        if (previous is null)
        {
            logger.LogInformation("Film search push: no watermark saved yet, so every film is pushed.");
            return true;
        }

        if (await searchIndex.CountFilmDocuments(cancellationToken) == 0)
        {
            logger.LogWarning(
                "Film search push: the index holds no film documents (rebuilt or torn down?), so every film is pushed instead of films changed since {Watermark}.",
                previous);
            return true;
        }

        return false;
    }

    private static long? Max(long? left, long? right) =>
        left is null ? right : right is null ? left : Math.Max(left.Value, right.Value);
}
