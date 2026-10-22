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
/// failed run is retried in full next time. A dry run writes nothing.</item>
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
            var orphans = (await searchIndex.GetFilmDocumentIds(cancellationToken))
                .Where(id => !existing.Contains(id))
                .ToList();
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

        foreach (var batch in toPush.Chunk(request.BatchSize))
        {
            failed += (await searchIndex.MergeOrUpload(batch, cancellationToken)).Count;
        }

        foreach (var batch in toDelete.Chunk(request.BatchSize))
        {
            failed += (await searchIndex.Delete(batch, cancellationToken)).Count;
        }

        if (failed > 0)
        {
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
