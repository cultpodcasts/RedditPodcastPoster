using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Api.Models;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;

namespace Api.Services.Podcasts;

/// <summary>
/// Transfers a podcast parent and its episodes to TV or News. Playables keep Services only; before mapping,
/// each episode gets deterministic <c>services.*.url</c> from nested ids via
/// <see cref="EpisodeServicePresence.EnsureServiceUrlsFromIds"/> (no Spotify / Apple / YouTube client calls).
/// Client materialisation remains on single-episode Move, where one episode's quota cost is acceptable.
/// <para>
/// Only one transfer per podcast runs at a time: a LookUps marker (<see cref="IParentKindTransferLockStore"/>) is
/// taken first, and a second request while it is fresh gets <see cref="PodcastKindTransferStatus.AlreadyRunning"/>.
/// The holder renews the marker every <see cref="LockHeartbeatInterval"/> while it works; a marker with no heartbeat
/// for <see cref="LockStaleAfter"/> belongs to a transfer that died, and the next request takes it over and resumes.
/// A holder whose renewal finds the marker taken over stops at once (without compensating: the new holder owns the
/// rows). Resume is safe because destination writes are upserts under the source guids, an existing destination
/// parent of the requested kind is reused, source deletes skip rows already gone, and the podcast row is deleted
/// last, only once a re-read finds no episodes left (episodes that arrive mid-transfer are moved too).
/// </para>
/// <para>
/// Reads honour the request's cancellation token; once writing starts it is no longer honoured, because a caller
/// that disconnects must not stop a transfer half way. Destination playables are saved and source episodes deleted
/// with bounded parallelism (<see cref="CatalogueBulkWrite"/>), retrying Cosmos throttling. The home page active
/// count is not touched: it counts the destination playables too, so a transfer changes it by nothing.
/// </para>
/// </summary>
public class PodcastKindTransferService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IParentKindTransferLockStore lockStore,
    TimeProvider timeProvider,
    ILogger<PodcastKindTransferService> logger) : IPodcastKindTransferService
{
    /// <summary>A marker with no heartbeat for this long belongs to a transfer that died.</summary>
    public static readonly TimeSpan LockStaleAfter = TimeSpan.FromMinutes(10);

    /// <summary>How often the holder renews its marker while it works.</summary>
    public static readonly TimeSpan LockHeartbeatInterval = TimeSpan.FromMinutes(1);

    /// <summary>Source episodes per bulk delete call; the marker is renewed between calls.</summary>
    public const int SourceDeleteChunkSize = 200;

    /// <summary>Extra passes that move episodes which arrived during the transfer before giving up.</summary>
    public const int MaxLateArrivalPasses = 2;

    public async Task<PodcastKindTransferResult> TransferAsync(
        Guid podcastId,
        PodcastKindTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TargetKind is not { } targetKind)
        {
            return new PodcastKindTransferResult(PodcastKindTransferStatus.InvalidTarget);
        }

        ParentKindTransferLockAcquisition acquisition;
        try
        {
            acquisition = await lockStore.TryAcquire(podcastId, targetKind, UtcNow(), LockStaleAfter);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to take the transfer marker for podcast '{podcastId}'.",
                nameof(TransferAsync), podcastId);
            return new PodcastKindTransferResult(PodcastKindTransferStatus.Failed, podcastId);
        }

        if (acquisition.Handle is not { } handle)
        {
            logger.LogInformation(
                "{method}: A kind transfer of podcast '{podcastId}' is already running (since {runningSinceUtc:O}).",
                nameof(TransferAsync), podcastId, acquisition.RunningSinceUtc);
            return new PodcastKindTransferResult(PodcastKindTransferStatus.AlreadyRunning, podcastId, targetKind);
        }

        var lease = new TransferLease(lockStore, timeProvider, handle, logger);
        try
        {
            if (acquisition.Outcome == ParentKindTransferLockOutcome.ResumedStale)
            {
                logger.LogWarning(
                    "{method}: Resuming kind transfer of podcast '{podcastId}' to {targetKind} after a stale marker " +
                    "from {runningSinceUtc:O} (that transfer was to {previousTargetKind}).",
                    nameof(TransferAsync), podcastId, targetKind, acquisition.RunningSinceUtc,
                    acquisition.PreviousTargetKind);
            }

            return await TransferUnderLock(
                podcastId,
                targetKind,
                acquisition.PreviousTargetKind,
                lease,
                cancellationToken);
        }
        finally
        {
            await lockStore.Release(lease.Handle);
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private sealed record Snapshot(
        Podcast? Podcast,
        TvShow? ExistingTvShow,
        NewsOrganisation? ExistingNewsOrganisation,
        IReadOnlyList<Episode> Episodes);

    private async Task<PodcastKindTransferResult> TransferUnderLock(
        Guid podcastId,
        CatalogueParentKind targetKind,
        CatalogueParentKind? previousTargetKind,
        TransferLease lease,
        CancellationToken cancellationToken)
    {
        Snapshot snapshot;
        try
        {
            snapshot = await ReadSnapshot(podcastId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Only the read phase honours the token, so nothing was written.
            logger.LogInformation(
                "{method}: Caller cancelled the kind transfer of podcast '{podcastId}' before any write.",
                nameof(TransferAsync), podcastId);
            return new PodcastKindTransferResult(PodcastKindTransferStatus.Failed, podcastId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to read podcast '{podcastId}' for a kind transfer.",
                nameof(TransferAsync), podcastId);
            return new PodcastKindTransferResult(PodcastKindTransferStatus.Failed, podcastId);
        }

        if (snapshot.Podcast is not { } podcast)
        {
            return snapshot.ExistingTvShow is not null || snapshot.ExistingNewsOrganisation is not null
                ? new PodcastKindTransferResult(PodcastKindTransferStatus.Conflict, podcastId, targetKind)
                : new PodcastKindTransferResult(PodcastKindTransferStatus.NotFound, podcastId);
        }

        var siblingParentExists = targetKind switch
        {
            CatalogueParentKind.TvShow => snapshot.ExistingNewsOrganisation is not null,
            CatalogueParentKind.NewsOrganisation => snapshot.ExistingTvShow is not null,
            _ => false
        };
        if (siblingParentExists)
        {
            if (previousTargetKind is { } previous && previous != targetKind)
            {
                logger.LogWarning(
                    "{method}: An earlier kind transfer of podcast '{podcastId}' to {previousTargetKind} did not " +
                    "finish; re-run the transfer to {previousTargetKind} to complete it.",
                    nameof(TransferAsync), podcastId, previous, previous);
            }

            return new PodcastKindTransferResult(PodcastKindTransferStatus.Conflict, podcastId, targetKind);
        }

        // From here on the transfer writes; the caller's token is no longer honoured, and any exception (including
        // a client-side Cosmos or HTTP timeout surfacing as OperationCanceledException) is a real failure.
        try
        {
            return targetKind switch
            {
                CatalogueParentKind.TvShow => await TransferParent(
                    podcast, snapshot.Episodes, resuming: snapshot.ExistingTvShow is not null, TvShowTarget(), lease),
                CatalogueParentKind.NewsOrganisation => await TransferParent(
                    podcast, snapshot.Episodes, resuming: snapshot.ExistingNewsOrganisation is not null,
                    NewsOrganisationTarget(), lease),
                _ => new PodcastKindTransferResult(PodcastKindTransferStatus.InvalidTarget)
            };
        }
        catch (TransferLeaseLostException)
        {
            logger.LogError(
                "{method}: Another request took over the kind transfer of podcast '{podcastId}'; this attempt stopped.",
                nameof(TransferAsync), podcastId);
            return new PodcastKindTransferResult(PodcastKindTransferStatus.AlreadyRunning, podcastId, targetKind);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to transfer podcast '{podcastId}'.", nameof(TransferAsync), podcastId);
            return new PodcastKindTransferResult(PodcastKindTransferStatus.Failed, podcastId);
        }
    }

    private async Task<Snapshot> ReadSnapshot(Guid podcastId, CancellationToken cancellationToken)
    {
        var podcast = await podcastRepository.GetPodcast(podcastId);
        var existingTvShow = await tvShowRepository.GetTvShow(podcastId);
        var existingNewsOrganisation = await newsOrganisationRepository.GetNewsOrganisation(podcastId);
        var episodes = podcast is null ? [] : await ReadEpisodes(podcastId, cancellationToken);
        return new Snapshot(podcast, existingTvShow, existingNewsOrganisation, episodes);
    }

    private async Task<List<Episode>> ReadEpisodes(Guid podcastId, CancellationToken cancellationToken)
    {
        var episodes = new List<Episode>();
        await foreach (var episode in episodeRepository.GetByPodcastId(podcastId).WithCancellation(cancellationToken))
        {
            episodes.Add(episode);
        }

        return episodes;
    }

    private sealed record TransferTarget<TParent, TPlayable>(
        CatalogueParentKind Kind,
        string SearchContentKind,
        Func<Podcast, TParent> MapParent,
        Func<Episode, TParent, TPlayable> MapPlayable,
        Func<TParent, Task> SaveParent,
        Func<TPlayable, Task> SavePlayable,
        Func<Guid, Guid, Task> DeletePlayable,
        Func<Guid, Task> DeleteParent)
        where TParent : class
        where TPlayable : Playable;

    private TransferTarget<TvShow, TvShowEpisode> TvShowTarget() =>
        new(
            CatalogueParentKind.TvShow,
            SearchContentKind.TvShowEpisode,
            CatalogueParentKindMapper.ToTvShow,
            CatalogueParentKindMapper.ToTvShowEpisode,
            tvShowRepository.Save,
            tvShowEpisodeRepository.Save,
            tvShowEpisodeRepository.Delete,
            tvShowRepository.Delete);

    private TransferTarget<NewsOrganisation, NewsReport> NewsOrganisationTarget() =>
        new(
            CatalogueParentKind.NewsOrganisation,
            SearchContentKind.NewsReport,
            CatalogueParentKindMapper.ToNewsOrganisation,
            CatalogueParentKindMapper.ToNewsReport,
            newsOrganisationRepository.Save,
            newsReportRepository.Save,
            newsReportRepository.Delete,
            newsOrganisationRepository.Delete);

    /// <summary>Per-transfer counters for the one summary log line.</summary>
    private sealed class TransferStats
    {
        private int _throttleRetries;
        public TimeSpan WriteElapsed;
        public TimeSpan SearchElapsed;
        public TimeSpan DeleteElapsed;
        public int Deleted;
        public int Missing;
        public double DeleteRequestCharge;
        public int ThrottleRetries => Volatile.Read(ref _throttleRetries);
        public void Throttled() => Interlocked.Increment(ref _throttleRetries);

        public void Add(EpisodeBulkDeleteResult result)
        {
            Deleted += result.Deleted;
            Missing += result.Missing;
            DeleteRequestCharge += result.RequestCharge;
            Interlocked.Add(ref _throttleRetries, result.ThrottleRetries);
        }
    }

    private async Task<PodcastKindTransferResult> TransferParent<TParent, TPlayable>(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        bool resuming,
        TransferTarget<TParent, TPlayable> target,
        TransferLease lease)
        where TParent : class
        where TPlayable : Playable
    {
        var total = Stopwatch.StartNew();
        var stats = new TransferStats();
        var parent = target.MapParent(podcast);
        await CatalogueBulkWrite.RetryThrottled(() => target.SaveParent(parent), stats.Throttled);

        var batch = episodes;
        var moved = 0;
        var indexed = true;
        var passes = 0;
        for (var pass = 0; ; pass++)
        {
            passes = pass + 1;
            // A resumed transfer, or any later pass, must not compensate away the parent: playables moved earlier
            // (whose source rows may already be gone) would be orphaned.
            var swap = await MovePass(podcast, parent, batch, target, lease, stats, compensateParent: pass == 0 && !resuming);
            moved += batch.Count;
            indexed &= swap.Indexed;
            if (!swap.DeleteSourceRows)
            {
                // A failed delete of a removed key cannot be retried once the source row is gone: the pull indexer
                // never sees a removed destination playable, and this index has no soft-delete. Keep the source.
                LogSummary(podcast.Id, target.Kind, moved, passes, stats, total.Elapsed, podcastDeleted: false);
                return new PodcastKindTransferResult(
                    PodcastKindTransferStatus.Accepted, podcast.Id, target.Kind, moved, FailureIndexingPlayables: true);
            }

            // Episodes can arrive while the transfer runs (Indexer, submit, discovery). Deleting the podcast row
            // would orphan them, so move them too before the podcast goes.
            batch = await ReadEpisodes(podcast.Id, CancellationToken.None);
            if (batch.Count == 0)
            {
                break;
            }

            if (pass >= MaxLateArrivalPasses)
            {
                logger.LogWarning(
                    "{method}: {count} episodes of podcast '{podcastId}' are still arriving after {passes} passes; " +
                    "keeping the podcast row. Re-run the transfer to finish.",
                    nameof(TransferParent), batch.Count, podcast.Id, pass + 1);
                LogSummary(podcast.Id, target.Kind, moved, passes, stats, total.Elapsed, podcastDeleted: false);
                return new PodcastKindTransferResult(
                    PodcastKindTransferStatus.Incomplete, podcast.Id, target.Kind, moved, !indexed);
            }

            logger.LogInformation(
                "{method}: {count} episodes of podcast '{podcastId}' arrived during the transfer; moving them.",
                nameof(TransferParent), batch.Count, podcast.Id);
        }

        await lease.Heartbeat();
        await CatalogueBulkWrite.RetryThrottled(() => podcastRepository.Delete(podcast.Id), stats.Throttled);
        LogSummary(podcast.Id, target.Kind, moved, passes, stats, total.Elapsed, podcastDeleted: true);
        return new PodcastKindTransferResult(
            PodcastKindTransferStatus.Accepted, podcast.Id, target.Kind, moved, FailureIndexingPlayables: !indexed);
    }

    private async Task<SearchSwapResult> MovePass<TParent, TPlayable>(
        Podcast podcast,
        TParent parent,
        IReadOnlyList<Episode> episodes,
        TransferTarget<TParent, TPlayable> target,
        TransferLease lease,
        TransferStats stats,
        bool compensateParent)
        where TParent : class
        where TPlayable : Playable
    {
        // Playables keep Services only. Fill missing services.*.url from nested ids (deterministic, no clients) so
        // a large podcast transfer stays Cosmos/search I/O and does not sequentially hit platform APIs.
        foreach (var episode in episodes)
        {
            EpisodeServicePresence.EnsureServiceUrlsFromIds(episode, podcast.AppleId);
        }

        var playables = episodes.Select(episode => target.MapPlayable(episode, parent)).ToList();
        var writtenPlayableIds = new ConcurrentBag<Guid>();
        var phase = Stopwatch.StartNew();
        try
        {
            await Parallel.ForEachAsync(
                playables,
                new ParallelOptions { MaxDegreeOfParallelism = CatalogueBulkWrite.Parallelism },
                async (playable, _) =>
                {
                    await lease.Heartbeat();
                    await CatalogueBulkWrite.RetryThrottled(() => target.SavePlayable(playable), stats.Throttled);
                    writtenPlayableIds.Add(playable.Id);
                });
        }
        catch (Exception ex) when (ex is not TransferLeaseLostException)
        {
            logger.LogError(
                ex,
                "{method}: Dest write failed for podcast '{podcastId}' to {targetKind}; compensating dest rows.",
                nameof(MovePass),
                podcast.Id,
                target.Kind);
            await CompensateDest(podcast.Id, compensateParent, writtenPlayableIds.ToList(), target);
            throw;
        }

        stats.WriteElapsed += phase.Elapsed;

        await lease.Heartbeat();
        phase.Restart();
        var swap = await SwapSearch(podcast, episodes, target.SearchContentKind);
        stats.SearchElapsed += phase.Elapsed;
        if (!swap.DeleteSourceRows)
        {
            return swap;
        }

        // Source rows go in chunks so the marker is renewed between them; the podcast row stays until the end.
        phase.Restart();
        foreach (var chunk in episodes.Chunk(SourceDeleteChunkSize))
        {
            await lease.Heartbeat();
            stats.Add(await episodeRepository.DeleteMany(podcast.Id, chunk));
        }

        stats.DeleteElapsed += phase.Elapsed;
        return swap;
    }

    private void LogSummary(
        Guid podcastId,
        CatalogueParentKind targetKind,
        int moved,
        int passes,
        TransferStats stats,
        TimeSpan elapsed,
        bool podcastDeleted)
    {
        logger.LogWarning(
            "{method}: Kind transfer of podcast '{podcastId}' to {targetKind}: {moved} playables in {elapsedMs} ms " +
            "(writes {writeMs} ms, search {searchMs} ms, source deletes {deleteMs} ms); deleted {deleted}, already gone " +
            "{missing}, delete RU {deleteRequestCharge:F1}, throttle retries {throttleRetries}, passes " +
            "{passes}, podcast deleted {podcastDeleted}.",
            nameof(TransferParent),
            podcastId,
            targetKind,
            moved,
            (long)elapsed.TotalMilliseconds,
            (long)stats.WriteElapsed.TotalMilliseconds,
            (long)stats.SearchElapsed.TotalMilliseconds,
            (long)stats.DeleteElapsed.TotalMilliseconds,
            stats.Deleted,
            stats.Missing,
            stats.DeleteRequestCharge,
            stats.ThrottleRetries,
            passes,
            podcastDeleted);
    }

    private async Task CompensateDest<TParent, TPlayable>(
        Guid parentId,
        bool compensateParent,
        IReadOnlyList<Guid> writtenPlayableIds,
        TransferTarget<TParent, TPlayable> target)
        where TParent : class
        where TPlayable : Playable
    {
        await Parallel.ForEachAsync(
            writtenPlayableIds,
            new ParallelOptions { MaxDegreeOfParallelism = CatalogueBulkWrite.Parallelism },
            async (playableId, _) =>
            {
                try
                {
                    await CatalogueBulkWrite.RetryThrottled(() => target.DeletePlayable(parentId, playableId));
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    logger.LogDebug(
                        "{method}: Dest playable '{playableId}' for parent '{parentId}' was already gone.",
                        nameof(CompensateDest), playableId, parentId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "{method}: Failed to compensate dest playable '{playableId}' for parent '{parentId}'.",
                        nameof(CompensateDest),
                        playableId,
                        parentId);
                }
            });

        if (!compensateParent)
        {
            return;
        }

        try
        {
            await CatalogueBulkWrite.RetryThrottled(() => target.DeleteParent(parentId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "{method}: Failed to compensate dest parent '{parentId}'.",
                nameof(CompensateDest),
                parentId);
        }
    }

    private readonly record struct SearchSwapResult(bool Indexed, bool DeleteSourceRows);

    private async Task<SearchSwapResult> SwapSearch(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        string contentKind)
    {
        var excluded = episodes
            .Where(episode => EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(podcast, episode))
            .Select(episode => episode.Id)
            .ToList();
        var documents = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(podcast, episodes, contentKind);
        var uploaded = await searchSwap.UploadAsync(documents, CancellationToken.None);
        if (excluded.Count == 0)
        {
            return new SearchSwapResult(uploaded, DeleteSourceRows: true);
        }

        // A removed episode may already be in the index. Do not rewrite it as the new kind; drop that key.
        // An empty excluded set still drops the source, because a kept id can be healed by the next pull.
        var deleted = await searchSwap.DeleteAsync(excluded, CancellationToken.None);
        return new SearchSwapResult(uploaded && deleted, DeleteSourceRows: deleted);
    }

    /// <summary>Thrown when a heartbeat finds the marker taken over: this attempt must stop writing.</summary>
    private sealed class TransferLeaseLostException(Guid podcastId)
        : Exception($"The kind transfer marker for podcast '{podcastId}' was taken over.");

    /// <summary>
    /// The held marker. <see cref="Heartbeat"/> renews it at most once per <see cref="LockHeartbeatInterval"/>
    /// (progress-driven, so no timer) and throws <see cref="TransferLeaseLostException"/> once it is lost.
    /// </summary>
    private sealed class TransferLease(
        IParentKindTransferLockStore store,
        TimeProvider timeProvider,
        ParentKindTransferLockHandle handle,
        ILogger logger)
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private DateTime _lastRenewedUtc = handle.StartedUtc;
        private volatile bool _lost;

        public ParentKindTransferLockHandle Handle { get; private set; } = handle;

        public async Task Heartbeat()
        {
            if (_lost)
            {
                throw new TransferLeaseLostException(Handle.PodcastId);
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (now - _lastRenewedUtc < LockHeartbeatInterval || !await _gate.WaitAsync(0))
            {
                return;
            }

            try
            {
                var renewed = await store.Renew(Handle, now);
                if (renewed is null)
                {
                    _lost = true;
                    throw new TransferLeaseLostException(Handle.PodcastId);
                }

                Handle = renewed;
                _lastRenewedUtc = now;
            }
            catch (Exception ex) when (ex is not TransferLeaseLostException)
            {
                // A transient renewal failure: the marker is still ours; try again on the next heartbeat.
                logger.LogWarning(ex, "Failed to renew the kind transfer marker for podcast '{podcastId}'.",
                    Handle.PodcastId);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
