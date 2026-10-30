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
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;

namespace Api.Services.Podcasts;

/// <summary>
/// Transfers a podcast parent and its episodes to TV or News. Playables keep Services only; before mapping,
/// each episode gets deterministic <c>services.*.url</c> from nested ids via
/// <see cref="EpisodeServicePresence.EnsureServiceUrlsFromIds"/> (no Spotify / Apple / YouTube client calls).
/// Client materialisation remains on single-episode Move, where one episode's quota cost is acceptable.
/// </summary>
public class PodcastKindTransferService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IFileKeyAllocator fileKeyAllocator,
    ILogger<PodcastKindTransferService> logger) : IPodcastKindTransferService
{
    public async Task<PodcastKindTransferResult> TransferAsync(
        Guid podcastId,
        PodcastKindTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TargetKind is not { } targetKind)
        {
            return new PodcastKindTransferResult(PodcastKindTransferStatus.InvalidTarget);
        }

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

            return targetKind switch
            {
                CatalogueParentKind.TvShow =>
                    await TransferToTvShow(podcast, episodes, existingNewsOrganisation is not null, cancellationToken),
                CatalogueParentKind.NewsOrganisation =>
                    await TransferToNewsOrganisation(podcast, episodes, existingTvShow is not null, cancellationToken),
                _ => new PodcastKindTransferResult(PodcastKindTransferStatus.InvalidTarget)
            };
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
        CancellationToken cancellationToken) =>
        TransferParent(
            podcast,
            episodes,
            siblingParentExists,
            CatalogueParentKind.TvShow,
            FileKeyKind.TvShow,
            SearchContentKind.TvShowEpisode,
            CatalogueParentKindMapper.ToTvShow,
            CatalogueParentKindMapper.ToTvShowEpisode,
            tvShowRepository.Save,
            tvShowEpisodeRepository.Save,
            tvShowEpisodeRepository.Delete,
            tvShowRepository.Delete,
            cancellationToken);

    private Task<PodcastKindTransferResult> TransferToNewsOrganisation(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        bool siblingParentExists,
        CancellationToken cancellationToken) =>
        TransferParent(
            podcast,
            episodes,
            siblingParentExists,
            CatalogueParentKind.NewsOrganisation,
            FileKeyKind.NewsOrganisation,
            SearchContentKind.NewsReport,
            CatalogueParentKindMapper.ToNewsOrganisation,
            CatalogueParentKindMapper.ToNewsReport,
            newsOrganisationRepository.Save,
            newsReportRepository.Save,
            newsReportRepository.Delete,
            newsOrganisationRepository.Delete,
            cancellationToken);

    private async Task<PodcastKindTransferResult> TransferParent<TParent, TPlayable>(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        bool siblingParentExists,
        CatalogueParentKind targetKind,
        FileKeyKind fileKeyKind,
        string searchContentKind,
        Func<Podcast, TParent> mapParent,
        Func<Episode, TParent, TPlayable> mapPlayable,
        Func<TParent, Task> saveParent,
        Func<TPlayable, Task> savePlayable,
        Func<Guid, Guid, Task> deletePlayable,
        Func<Guid, Task> deleteParent,
        CancellationToken cancellationToken)
        where TParent : Publisher
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
        // Never empty and unique among the target kind: a name with no usable Latin slug keeps the podcast's own
        // usable key (with the kind prefix), else gets an id key.
        parent.FileKey = await fileKeyAllocator.Allocate(fileKeyKind, parent.Name, parent.Id, podcast.FileKey);
        var playables = episodes
            .Select(episode => mapPlayable(episode, parent))
            .ToList();

        var writtenPlayableIds = new List<Guid>();
        var parentWritten = false;
        try
        {
            await saveParent(parent);
            parentWritten = true;
            foreach (var playable in playables)
            {
                await savePlayable(playable);
                writtenPlayableIds.Add(playable.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{method}: Dest write failed for podcast '{podcastId}' to {targetKind}; compensating dest rows.",
                nameof(TransferParent),
                podcast.Id,
                targetKind);
            await CompensateDest(podcast.Id, parentWritten, writtenPlayableIds, deletePlayable, deleteParent);
            throw;
        }

        var swap = await SwapSearch(podcast, episodes, searchContentKind, cancellationToken);
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
        string contentKind,
        CancellationToken cancellationToken)
    {
        var excluded = episodes
            .Where(episode => EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(podcast, episode))
            .Select(episode => episode.Id)
            .ToList();
        var documents = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(podcast, episodes, contentKind);
        var uploaded = await searchSwap.UploadAsync(documents, cancellationToken);
        if (excluded.Count == 0)
        {
            return new SearchSwapResult(uploaded, DeleteSourceRows: true);
        }

        // A removed episode may already be in the index. Do not rewrite it as the new kind; drop that key.
        var deleted = await searchSwap.DeleteAsync(excluded, cancellationToken);
        return new SearchSwapResult(uploaded && deleted, DeleteSourceRows: deleted);
    }

    private async Task DeletePodcastRows(Podcast podcast, IReadOnlyList<Episode> episodes)
    {
        foreach (var episode in episodes)
        {
            await episodeRepository.Delete(podcast.Id, episode.Id);
        }

        await podcastRepository.Delete(podcast.Id);
    }
}
