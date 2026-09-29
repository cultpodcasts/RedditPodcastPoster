using Microsoft.Extensions.Logging;
using Api.Models;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
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
            if (podcast is null)
            {
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
                    await TransferToTvShow(podcast, episodes, cancellationToken),
                CatalogueParentKind.NewsOrganisation =>
                    await TransferToNewsOrganisation(podcast, episodes, cancellationToken),
                _ => new PodcastKindTransferResult(PodcastKindTransferStatus.InvalidTarget)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to transfer podcast '{podcastId}'.", nameof(TransferAsync), podcastId);
            return new PodcastKindTransferResult(PodcastKindTransferStatus.Failed, podcastId);
        }
    }

    private async Task<PodcastKindTransferResult> TransferToTvShow(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        CancellationToken cancellationToken)
    {
        var existing = await tvShowRepository.GetTvShow(podcast.Id);
        if (existing is not null)
        {
            return new PodcastKindTransferResult(
                PodcastKindTransferStatus.Conflict,
                podcast.Id,
                CatalogueParentKind.TvShow);
        }

        var show = CatalogueParentKindMapper.ToTvShow(podcast);
        var playables = episodes
            .Select(episode => CatalogueParentKindMapper.ToTvShowEpisode(episode, show))
            .ToList();

        await tvShowRepository.Save(show);
        foreach (var playable in playables)
        {
            await tvShowEpisodeRepository.Save(playable);
        }

        var indexed = await SwapSearch(podcast, episodes, SearchContentKind.TvShowEpisode, cancellationToken);
        await DeletePodcastRows(podcast, episodes);

        return new PodcastKindTransferResult(
            PodcastKindTransferStatus.Accepted,
            show.Id,
            CatalogueParentKind.TvShow,
            playables.Count,
            FailureIndexingPlayables: !indexed);
    }

    private async Task<PodcastKindTransferResult> TransferToNewsOrganisation(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        CancellationToken cancellationToken)
    {
        var existing = await newsOrganisationRepository.GetNewsOrganisation(podcast.Id);
        if (existing is not null)
        {
            return new PodcastKindTransferResult(
                PodcastKindTransferStatus.Conflict,
                podcast.Id,
                CatalogueParentKind.NewsOrganisation);
        }

        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        var playables = episodes
            .Select(episode => CatalogueParentKindMapper.ToNewsReport(episode, organisation))
            .ToList();

        await newsOrganisationRepository.Save(organisation);
        foreach (var playable in playables)
        {
            await newsReportRepository.Save(playable);
        }

        var indexed = await SwapSearch(podcast, episodes, SearchContentKind.NewsReport, cancellationToken);
        await DeletePodcastRows(podcast, episodes);

        return new PodcastKindTransferResult(
            PodcastKindTransferStatus.Accepted,
            organisation.Id,
            CatalogueParentKind.NewsOrganisation,
            playables.Count,
            FailureIndexingPlayables: !indexed);
    }

    private async Task<bool> SwapSearch(
        Podcast podcast,
        IReadOnlyList<Episode> episodes,
        string contentKind,
        CancellationToken cancellationToken)
    {
        var documents = new List<EpisodeSearchRecord>(episodes.Count);
        foreach (var episode in episodes)
        {
            var record = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);
            record.ContentKind = contentKind;
            documents.Add(record);
        }

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
