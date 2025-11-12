using Azure;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Search;

namespace RedditPodcastPoster.EntitySearchIndexer;

public class EpisodeSearchIndexerService(
    IPodcastRepository podcastRepository,
    SearchClient searchClient,
    ILogger<EpisodeSearchIndexerService> logger) : IEpisodeSearchIndexerService
{
    public async Task<EntitySearchIndexerResponse> IndexEpisode(Guid episodeId, CancellationToken c)
    {
        var podcast = await podcastRepository.GetBy(x => x.Episodes.Any(e => e.Id == episodeId));
        if (podcast == null)
        {
            logger.LogError("Unable to find episode to reindex. Episode-id: '{episodeId}'.", episodeId);
            return new EntitySearchIndexerResponse
                { EpisodeIndexRequestState = EpisodeIndexRequestState.EpisodeNotFound };
        }

        var episode = podcast.Episodes.Where(e => e.Id == episodeId);
        if (episode.Count() > 1)
        {
            logger.LogError("Multiple episodes with Episode-id: '{episodeId}'.", episodeId);
            return new EntitySearchIndexerResponse
                { EpisodeIndexRequestState = EpisodeIndexRequestState.EpisodeIdConflict };
        }

        var document = new PodcastEpisode(podcast, podcast.Episodes.Single(x => x.Id == episodeId))
            .ToEpisodeSearchRecord();

        try
        {
            var result =
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
            var podcastId =
                await podcastRepository.GetBy(x => x.Episodes.Any(e => e.Id == episodeId), p => new { Id = p.Id });
            if (podcastId == null)
            {
                logger.LogError("Unable to find episode to reindex. Episode-id: '{episodeId}'.", episodeId);
                continue;
            }

            Podcast podcast;
            if (podcasts.ContainsKey(podcastId!.Id))
            {
                podcast = podcasts[podcastId.Id];
            }
            else
            {
                podcast = (await podcastRepository.GetBy(x => x.Id == podcastId.Id))!;
                podcasts.Add(podcastId.Id, podcast);
            }

            var episode = podcast.Episodes.Where(e => e.Id == episodeId);
            if (episode.Count() > 1)
            {
                logger.LogError("Multiple episodes with Episode-id: '{episodeId}'.", episodeId);
                continue;
            }

            var document = new PodcastEpisode(podcast, podcast.Episodes.Single(x => x.Id == episodeId))
                .ToEpisodeSearchRecord();
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
        else
        {
            logger.LogWarning("No documents to update in search-index");
            return new EntitySearchIndexerResponse { EpisodeIndexRequestState = EpisodeIndexRequestState.NoDocuments };
        }
    }
}