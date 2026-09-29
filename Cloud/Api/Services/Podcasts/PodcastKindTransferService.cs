using Microsoft.Extensions.Logging;
using Api.Models;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;

namespace Api.Services.Podcasts;

public class PodcastKindTransferService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IPodcastKindSearchSwap searchSwap,
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
        string searchContentKind,
        Func<Podcast, TParent> mapParent,
        Func<Episode, TParent, TPlayable> mapPlayable,
        Func<TParent, Task> saveParent,
        Func<TPlayable, Task> savePlayable,
        Func<Guid, Guid, Task> deletePlayable,
        Func<Guid, Task> deleteParent,
        CancellationToken cancellationToken)
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

        var parent = mapParent(podcast);
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

        var indexed = await SwapSearch(podcast, episodes, searchContentKind, cancellationToken);
        await DeletePodcastRows(podcast, episodes);

        return new PodcastKindTransferResult(
            PodcastKindTransferStatus.Accepted,
            podcast.Id,
            targetKind,
            playables.Count,
            FailureIndexingPlayables: !indexed);
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

    private async Task<bool> SwapSearch(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        string contentKind,
        CancellationToken cancellationToken)
    {
        var documents = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(podcast, episodes, contentKind);
        return await searchSwap.UploadAsync(documents, cancellationToken);
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
