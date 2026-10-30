using System.Collections.Concurrent;
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
/// A transfer that died leaves a marker that goes stale after <see cref="LockStaleAfter"/>; the next request takes it
/// over and resumes. Resume is safe because destination writes are upserts under the source guids, an existing
/// destination parent of the requested kind is reused, source deletes skip rows already gone, and the podcast row
/// is deleted last (so it still exists until every episode is gone).
/// </para>
/// <para>
/// Once writing starts the request's cancellation token is no longer honoured: a caller that disconnects must not
/// stop a transfer half way. Destination playables are saved with bounded parallelism and source episodes are
/// deleted in bulk (<see cref="IEpisodeRepository.DeleteForParentTransfer"/>).
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
    /// <summary>A marker older than this belongs to a transfer that died (the HTTP request limit is 230 s).</summary>
    public static readonly TimeSpan LockStaleAfter = TimeSpan.FromMinutes(10);

    /// <summary>Concurrent destination playable upserts per transfer.</summary>
    public const int PlayableWriteParallelism = 16;

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
            acquisition = await lockStore.TryAcquire(
                podcastId,
                targetKind,
                timeProvider.GetUtcNow().UtcDateTime,
                LockStaleAfter);
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

        try
        {
            if (acquisition.Outcome == ParentKindTransferLockOutcome.ResumedStale)
            {
                logger.LogWarning(
                    "{method}: Resuming kind transfer of podcast '{podcastId}' to {targetKind} after a stale marker from {runningSinceUtc:O}.",
                    nameof(TransferAsync), podcastId, targetKind, acquisition.RunningSinceUtc);
            }

            return await TransferUnderLock(podcastId, targetKind, cancellationToken);
        }
        finally
        {
            await lockStore.Release(handle);
        }
    }

    private async Task<PodcastKindTransferResult> TransferUnderLock(
        Guid podcastId,
        CatalogueParentKind targetKind,
        CancellationToken cancellationToken)
    {
        try
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            var existingTvShow = await tvShowRepository.GetTvShow(podcastId);
            var existingNewsOrganisation = await newsOrganisationRepository.GetNewsOrganisation(podcastId);

            if (podcast is null)
            {
                if (existingTvShow is not null || existingNewsOrganisation is not null)
                {
                    return new PodcastKindTransferResult(
                        PodcastKindTransferStatus.Conflict,
                        podcastId,
                        targetKind);
                }

                return new PodcastKindTransferResult(PodcastKindTransferStatus.NotFound, podcastId);
            }

            var episodes = new List<Episode>();
            await foreach (var episode in episodeRepository.GetByPodcastId(podcastId)
                               .WithCancellation(cancellationToken))
            {
                episodes.Add(episode);
            }

            // From here on the transfer writes; a caller that disconnects must not stop it half way.
            return targetKind switch
            {
                CatalogueParentKind.TvShow =>
                    await TransferToTvShow(
                        podcast,
                        episodes,
                        siblingParentExists: existingNewsOrganisation is not null,
                        resuming: existingTvShow is not null),
                CatalogueParentKind.NewsOrganisation =>
                    await TransferToNewsOrganisation(
                        podcast,
                        episodes,
                        siblingParentExists: existingTvShow is not null,
                        resuming: existingNewsOrganisation is not null),
                _ => new PodcastKindTransferResult(PodcastKindTransferStatus.InvalidTarget)
            };
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
            logger.LogError(ex, "{method}: Failed to transfer podcast '{podcastId}'.", nameof(TransferAsync), podcastId);
            return new PodcastKindTransferResult(PodcastKindTransferStatus.Failed, podcastId);
        }
    }

    private Task<PodcastKindTransferResult> TransferToTvShow(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        bool siblingParentExists,
        bool resuming) =>
        TransferParent(
            podcast,
            episodes,
            siblingParentExists,
            resuming,
            CatalogueParentKind.TvShow,
            SearchContentKind.TvShowEpisode,
            CatalogueParentKindMapper.ToTvShow,
            CatalogueParentKindMapper.ToTvShowEpisode,
            tvShowRepository.Save,
            tvShowEpisodeRepository.Save,
            tvShowEpisodeRepository.Delete,
            tvShowRepository.Delete);

    private Task<PodcastKindTransferResult> TransferToNewsOrganisation(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        bool siblingParentExists,
        bool resuming) =>
        TransferParent(
            podcast,
            episodes,
            siblingParentExists,
            resuming,
            CatalogueParentKind.NewsOrganisation,
            SearchContentKind.NewsReport,
            CatalogueParentKindMapper.ToNewsOrganisation,
            CatalogueParentKindMapper.ToNewsReport,
            newsOrganisationRepository.Save,
            newsReportRepository.Save,
            newsReportRepository.Delete,
            newsOrganisationRepository.Delete);

    private async Task<PodcastKindTransferResult> TransferParent<TParent, TPlayable>(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        bool siblingParentExists,
        bool resuming,
        CatalogueParentKind targetKind,
        string searchContentKind,
        Func<Podcast, TParent> mapParent,
        Func<Episode, TParent, TPlayable> mapPlayable,
        Func<TParent, Task> saveParent,
        Func<TPlayable, Task> savePlayable,
        Func<Guid, Guid, Task> deletePlayable,
        Func<Guid, Task> deleteParent)
        where TParent : class
        where TPlayable : Playable
    {
        if (siblingParentExists)
        {
            return new PodcastKindTransferResult(
                PodcastKindTransferStatus.Conflict,
                podcast.Id,
                targetKind);
        }

        // Playables keep Services only. Fill missing services.*.url from nested ids (deterministic, no clients) so
        // a large podcast transfer stays Cosmos/search I/O and does not sequentially hit platform APIs.
        foreach (var episode in episodes)
        {
            EpisodeServicePresence.EnsureServiceUrlsFromIds(episode, podcast.AppleId);
        }

        var parent = mapParent(podcast);
        var playables = episodes
            .Select(episode => mapPlayable(episode, parent))
            .ToList();

        var writtenPlayableIds = new ConcurrentBag<Guid>();
        var parentWritten = false;
        try
        {
            await saveParent(parent);
            // A resumed transfer must not compensate away a parent an earlier attempt wrote: its playables (whose
            // source rows may already be gone) would be orphaned.
            parentWritten = !resuming;
            await Parallel.ForEachAsync(
                playables,
                new ParallelOptions { MaxDegreeOfParallelism = PlayableWriteParallelism },
                async (playable, _) =>
                {
                    await savePlayable(playable);
                    writtenPlayableIds.Add(playable.Id);
                });
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{method}: Dest write failed for podcast '{podcastId}' to {targetKind}; compensating dest rows.",
                nameof(TransferParent),
                podcast.Id,
                targetKind);
            await CompensateDest(podcast.Id, parentWritten, writtenPlayableIds.ToList(), deletePlayable, deleteParent);
            throw;
        }

        var swap = await SwapSearch(podcast, episodes, searchContentKind);
        // A failed delete of a removed key cannot be retried once the source row is gone: the pull indexer
        // never sees a removed destination playable, and this index has no soft-delete. An empty excluded
        // set still drops the source, because a kept id can be healed by the next pull.
        if (swap.DeleteSourceRows)
        {
            await DeletePodcastRows(podcast, episodes);
        }

        return new PodcastKindTransferResult(
            PodcastKindTransferStatus.Accepted,
            podcast.Id,
            targetKind,
            playables.Count,
            FailureIndexingPlayables: !swap.Indexed);
    }

    private async Task CompensateDest(
        Guid parentId,
        bool parentWritten,
        IReadOnlyList<Guid> writtenPlayableIds,
        Func<Guid, Guid, Task> deletePlayable,
        Func<Guid, Task> deleteParent)
    {
        foreach (var playableId in writtenPlayableIds)
        {
            try
            {
                await deletePlayable(parentId, playableId);
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
        }

        if (!parentWritten)
        {
            return;
        }

        try
        {
            await deleteParent(parentId);
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
        var deleted = await searchSwap.DeleteAsync(excluded, CancellationToken.None);
        return new SearchSwapResult(uploaded && deleted, DeleteSourceRows: deleted);
    }

    private async Task DeletePodcastRows(Podcast podcast, IReadOnlyList<Episode> episodes)
    {
        // Episodes first, podcast last: if a delete fails the podcast row is still there, so a retry finds it and
        // resumes instead of reporting the transfer as already done with source episodes left behind.
        await episodeRepository.DeleteForParentTransfer(podcast.Id, episodes);
        await podcastRepository.Delete(podcast.Id);
    }
}
