using System.Net;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Search.Documents;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Services;

public class EpisodeSearchIndexerService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    SearchClient searchClient,
    ILogger<EpisodeSearchIndexerService> logger) : IEpisodeSearchIndexerService
{
    public Task<EntitySearchIndexerResponse> IndexEpisode(Guid episodeId, CancellationToken c) =>
        IndexEpisodeInternal(async () =>
        {
            var episode = await episodeRepository.GetBy(x => x.Id == episodeId);
            if (episode == null)
            {
                logger.LogError("Unable to find episode to reindex. Episode-id: '{episodeId}'.", episodeId);
                return null;
            }

            var podcast = await podcastRepository.GetPodcast(episode.PodcastId);
            if (podcast == null)
            {
                logger.LogError("Unable to find podcast to reindex. Podcast-id: '{podcastId}'.", episode.PodcastId);
                return null;
            }

            return new PodcastEpisode(podcast, episode);
        }, episodeId, c);

    public Task<EntitySearchIndexerResponse> IndexEpisode(
        Podcast podcast,
        Episode episode,
        CancellationToken c) =>
        IndexEpisodeInternal(() => Task.FromResult<PodcastEpisode?>(new PodcastEpisode(podcast, episode)),
            episode.Id, c);

    private async Task<EntitySearchIndexerResponse> IndexEpisodeInternal(
        Func<Task<PodcastEpisode?>> resolvePodcastEpisode,
        Guid episodeId,
        CancellationToken c)
    {
        var podcastEpisode = await resolvePodcastEpisode();
        if (podcastEpisode == null)
        {
            return new EntitySearchIndexerResponse
                { EpisodeIndexRequestState = EpisodeIndexRequestState.EpisodeNotFound };
        }

        try
        {
            if (EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(
                    podcastEpisode.Podcast, podcastEpisode.Episode))
            {
                await searchClient.DeleteDocumentsAsync(
                    "id",
                    [episodeId.ToString()],
                    new IndexDocumentsOptions { ThrowOnAnyError = true },
                    c);
                logger.LogInformation(
                    "Removed excluded episode '{episodeId}' (podcast '{podcastName}') from search-index.",
                    episodeId, podcastEpisode.Podcast.Name);
                return new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed };
            }

            var document = podcastEpisode.ToEpisodeSearchRecord();
            await searchClient.MergeOrUploadDocumentsAsync([document],
                new IndexDocumentsOptions { ThrowOnAnyError = true }, c);
            return new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed };
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex,
                "Failed to index episode with id '{episodeId}'. Status-code: {statusCode}, message: '{message}'.",
                episodeId, ex.Status, ex.Message);
            return new EntitySearchIndexerResponse { IndexerState = MapRequestFailedException(ex) };
        }
    }

    public async Task<EntitySearchIndexerResponse> IndexEpisodes(IEnumerable<Guid> episodeIds, CancellationToken c)
    {
        var documents = new List<EpisodeSearchRecord>();
        var deleteIds = new List<string>();
        var podcasts = new Dictionary<Guid, Podcast>();

        foreach (var episodeId in episodeIds)
        {
            var episode = await episodeRepository.GetBy(x => x.Id == episodeId);
            if (episode == null)
            {
                logger.LogError("Unable to find episode to reindex. Episode-id: '{episodeId}'.", episodeId);
                continue;
            }

            if (!podcasts.TryGetValue(episode.PodcastId, out var podcast))
            {
                podcast = await podcastRepository.GetPodcast(episode.PodcastId);
                if (podcast == null)
                {
                    logger.LogError("Unable to find podcast to reindex. Podcast-id: '{podcastId}'.", episode.PodcastId);
                    continue;
                }

                podcasts.Add(episode.PodcastId, podcast);
            }

            if (EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(podcast, episode))
            {
                deleteIds.Add(episodeId.ToString());
                continue;
            }

            documents.Add(new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord());
        }

        if (documents.Count == 0 && deleteIds.Count == 0)
        {
            logger.LogWarning("No documents to update in search-index");
            return new EntitySearchIndexerResponse { EpisodeIndexRequestState = EpisodeIndexRequestState.NoDocuments };
        }

        try
        {
            if (deleteIds.Count > 0)
            {
                var deleteResult = await searchClient.DeleteDocumentsAsync(
                    "id",
                    deleteIds,
                    new IndexDocumentsOptions { ThrowOnAnyError = false },
                    c);
                var deleteFailures = deleteResult.Value.Results.Where(x => !x.Succeeded).ToArray();
                foreach (var failure in deleteFailures)
                {
                    logger.LogError("Failed to delete search document '{Key}': {ErrorMessage}", failure.Key,
                        failure.ErrorMessage);
                }

                if (deleteFailures.Length > 0)
                {
                    var ex = new RequestFailedException(deleteResult.GetRawResponse());
                    logger.LogError(ex,
                        "Failed to delete {count} removed/excluded episode(s) from search-index.",
                        deleteFailures.Length);
                    return new EntitySearchIndexerResponse { IndexerState = MapRequestFailedException(ex) };
                }

                logger.LogInformation(
                    "Deleted {count} removed/excluded episode(s) from search-index.",
                    deleteIds.Count);
            }

            if (documents.Count > 0)
            {
                var result =
                    await searchClient.MergeOrUploadDocumentsAsync(documents,
                        new IndexDocumentsOptions { ThrowOnAnyError = false }, c);
                var failures = result.Value.Results.Where(x => x.Succeeded == false).ToArray();
                foreach (var failure in failures)
                {
                    logger.LogError("Failed to index episode with key '{Key}': {ErrorMessage}", failure.Key,
                        failure.ErrorMessage);
                }

                if (failures.Any())
                {
                    var ex = new RequestFailedException(result.GetRawResponse());
                    logger.LogError(ex,
                        "Failed to index episodes with id '{episodeIds}'. Status-code: {statusCode}, message: '{message}'.",
                        string.Join(", ", failures.Select(x => $"'{x.Key}'")), ex.Status, ex.Message);
                    return new EntitySearchIndexerResponse { IndexerState = MapRequestFailedException(ex) };
                }
            }

            return new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed };
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex,
                "Failed to index episodes. Status-code: {statusCode}, message: '{message}'.",
                ex.Status, ex.Message);
            return new EntitySearchIndexerResponse { IndexerState = MapRequestFailedException(ex) };
        }
    }

    private static IndexerState MapRequestFailedException(RequestFailedException ex) =>
        ex.Status == (int)HttpStatusCode.TooManyRequests
            ? IndexerState.TooManyRequests
            : IndexerState.Failure;
}
