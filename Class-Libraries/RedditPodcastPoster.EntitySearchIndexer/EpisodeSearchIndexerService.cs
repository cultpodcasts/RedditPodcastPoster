using Azure;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Search;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.EntitySearchIndexer;

public class EpisodeSearchIndexerService(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    SearchClient searchClient,
    ILogger<EpisodeSearchIndexerService> logger) : IEpisodeSearchIndexerService
{
    public async Task<EntitySearchIndexerResponse> IndexEpisode(Guid episodeId, CancellationToken c)
    {
        var episode = await episodeRepository.GetBy(x => x.Id == episodeId);
        if (episode == null)
        {
            logger.LogError("Unable to find episode to reindex. Episode-id: '{episodeId}'.", episodeId);
            return new EntitySearchIndexerResponse
                { EpisodeIndexRequestState = EpisodeIndexRequestState.EpisodeNotFound };
        }

        var podcast = await podcastRepository.GetPodcast(episode.PodcastId);
        if (podcast == null)
        {
            logger.LogError("Unable to find podcast to reindex. Podcast-id: '{podcastId}'.", episode.PodcastId);
            return new EntitySearchIndexerResponse
                { EpisodeIndexRequestState = EpisodeIndexRequestState.EpisodeNotFound };
        }

        var document = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord();

        try
        {
            await searchClient.MergeOrUploadDocumentsAsync([document],
                new IndexDocumentsOptions { ThrowOnAnyError = true }, c);
            return new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed };
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex,
                "Failed to index episode with id '{episodeId}'. Status-code: {statusCode}, message: '{message}'.",
                episodeId, ex.Status, ex.Message);
            return new EntitySearchIndexerResponse { IndexerState = IndexerState.Failure };
        }
    }

    public async Task<EntitySearchIndexerResponse> IndexEpisodes(IEnumerable<Guid> episodeIds, CancellationToken c)
    {
        var documents = new List<EpisodeSearchRecord>();
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

            var document = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord();
            documents.Add(document);
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
                return new EntitySearchIndexerResponse { IndexerState = IndexerState.Failure };
            }

            return new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed };
        }

        logger.LogWarning("No documents to update in search-index");
        return new EntitySearchIndexerResponse { EpisodeIndexRequestState = EpisodeIndexRequestState.NoDocuments };
    }
}
