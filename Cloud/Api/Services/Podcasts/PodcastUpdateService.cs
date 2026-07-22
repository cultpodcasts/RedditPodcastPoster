using System.Text.Json;
using Api.Dtos;
using Api.Models;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using DomainPodcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.Podcasts;

public class PodcastUpdateService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IEpisodeSearchIndexerService searchIndexerService,
    SearchClient searchClient,
    IShortnerService shortnerService,
    PodcastChangeApplier podcastChangeApplier,
    PodcastEpisodeProjectionHelper episodeProjectionHelper,
    ILogger<PodcastUpdateService> logger) : IPodcastUpdateService
{
    public async Task<PodcastUpdateResult> UpdateAsync(
        PodcastChangeRequestWrapper podcastChangeRequestWrapper,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation(
                "{method}: Podcast Change Request: episode-id: '{podcastId}'. {podcastChangeRequestWrapper}",
                nameof(UpdateAsync), podcastChangeRequestWrapper.PodcastId,
                JsonSerializer.Serialize(podcastChangeRequestWrapper.Podcast));
            var podcast = await podcastRepository.GetBy(x => x.Id == podcastChangeRequestWrapper.PodcastId);
            if (podcast == null)
            {
                logger.LogWarning("{method}: Podcast with id '{podcastId}' not found.", nameof(UpdateAsync),
                    podcastChangeRequestWrapper.PodcastId);
                return new PodcastUpdateResult(PodcastUpdateStatus.NotFound, podcastChangeRequestWrapper.PodcastId);
            }

            logger.LogInformation("{method}: Updating podcast-id '{podcastId}'.", nameof(UpdateAsync),
                podcastChangeRequestWrapper.PodcastId);

            podcastChangeApplier.Apply(podcast, podcastChangeRequestWrapper.Podcast);
            if (podcastChangeRequestWrapper.AllowNameChange &&
                !string.IsNullOrWhiteSpace(podcastChangeRequestWrapper.Podcast.Name))
            {
                await UpdateName(podcast, podcastChangeRequestWrapper.Podcast.Name);
            }

            await podcastRepository.Save(podcast);

            if (podcastChangeRequestWrapper.Podcast.Language != null)
            {
                await PropagatePodcastLanguageToEpisodes(podcast, c);
            }

            if (podcastChangeRequestWrapper.Podcast.Removed == true)
            {
                await episodeProjectionHelper.HydrateDetachedEpisodePodcastProjection(podcast, c);
            }

            var failureDeletingFromIndex = false;
            var failureIndexingEpisodes = false;
            if (podcastChangeRequestWrapper.Podcast.Removed.HasValue &&
                podcastChangeRequestWrapper.Podcast.Removed.Value)
            {
                failureDeletingFromIndex = !await DeleteEpisodesFromSearchIndex(c, podcast);
                await DeleteEpisodesFromShortner(podcast, c);
            }
            else if (podcastChangeRequestWrapper.AllowNameChange)
            {
                var episodeIds = await episodeProjectionHelper.GetEpisodeIdsByPodcastId(podcast.Id, c);
                if (episodeIds.Count > 0)
                {
                    try
                    {
                        await searchIndexerService.IndexEpisodes(episodeIds, c);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "{method}: Failed to re-index all episodes after podcast rename for podcast-id '{podcastId}'.",
                            nameof(UpdateAsync), podcastChangeRequestWrapper.PodcastId);
                        failureIndexingEpisodes = true;
                    }
                }
            }

            return new PodcastUpdateResult(
                PodcastUpdateStatus.Accepted,
                FailureIndexingEpisodes: failureIndexingEpisodes,
                FailureDeletingFromIndex: failureDeletingFromIndex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update podcast.", nameof(UpdateAsync));
            return new PodcastUpdateResult(PodcastUpdateStatus.Failed);
        }
    }

    private async Task PropagatePodcastLanguageToEpisodes(DomainPodcast podcast, CancellationToken c)
    {
        var podcastLanguage = podcast.Language?.Trim();
        if (string.IsNullOrWhiteSpace(podcastLanguage))
        {
            return;
        }

        var updatedEpisodeIds = new List<Guid>();
        await foreach (var episode in episodeRepository.GetByPodcastId(podcast.Id).WithCancellation(c))
        {
            if (!string.IsNullOrWhiteSpace(episode.Language))
            {
                continue;
            }

            episode.Language = podcastLanguage;
            episode.SetPodcastProperties(podcast);
            await episodeRepository.Save(episode);
            updatedEpisodeIds.Add(episode.Id);
        }

        if (updatedEpisodeIds.Count == 0)
        {
            return;
        }

        try
        {
            await searchIndexerService.IndexEpisodes(updatedEpisodeIds, c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed to re-index episodes after language propagation for podcast-id '{podcastId}'.",
                nameof(PropagatePodcastLanguageToEpisodes), podcast.Id);
        }
    }

    private async Task UpdateName(DomainPodcast podcast, string? podcastName)
    {
        if (string.IsNullOrWhiteSpace(podcastName))
        {
            throw new InvalidOperationException("Supplied podcast-name is null/empty");
        }

        var sameNamePodcasts =
            await podcastRepository.GetAllBy(x => x.Name == podcastName && x.Id != podcast.Id)
                .ToListAsync();
        if (sameNamePodcasts.Count > 0)
        {
            throw new InvalidOperationException(
                $"Other podcasts have requested name '{podcastName}'. Podcast-ids: {string.Join(", ", sameNamePodcasts.Select(x => $"'{x.Id}'"))}.");
        }

        podcast.Name = podcastName.Trim();
        podcast.FileKey = FileKeyFactory.GetFileKey(podcast.Name);
    }

    private async Task<bool> DeleteEpisodesFromSearchIndex(CancellationToken c, DomainPodcast podcast)
    {
        var failure = false;
        var episodeIds = (await episodeProjectionHelper.GetEpisodeIdsByPodcastId(podcast.Id, c))
            .Select(x => x.ToString())
            .ToArray();

        try
        {
            var result = await searchClient.DeleteDocumentsAsync("id", episodeIds,
                new IndexDocumentsOptions { ThrowOnAnyError = false }, c);
            failure = result.Value.Results.Any(x => !x.Succeeded);

            if (failure)
            {
                logger.LogError(
                    "Removed {successCount} documents. Failed to remove {failureCount} documents with search-index with ids: {documentIds}.",
                    result.Value.Results.Count(x => x.Succeeded),
                    result.Value.Results.Count(x => !x.Succeeded),
                    string.Join(",", episodeIds.Select(x => $"'{x}'")));
            }
            else
            {
                logger.LogInformation("Removed {successCount} documents. ",
                    result.Value.Results.Count(x => x.Succeeded));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Exception while deleting documents with search-index with ids: {documentIds}.",
                string.Join(",", episodeIds.Select(x => $"'{x}'")));
            failure = true;
        }

        return !failure;
    }

    private async Task DeleteEpisodesFromShortner(DomainPodcast podcast, CancellationToken c)
    {
        var detachedEpisodes = await episodeProjectionHelper.GetDetachedEpisodesByPodcastId(podcast.Id, c);

        var podcastEpisodes = detachedEpisodes.Select(e => new PodcastEpisode(podcast, e));

        await shortnerService.Delete(podcastEpisodes);
    }
}
