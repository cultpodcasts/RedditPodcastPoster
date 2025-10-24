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
    public async Task IndexEpisode(Guid episodeId, CancellationToken c)
    {
        var podcast = await podcastRepository.GetBy(x => x.Episodes.Any(e => e.Id == episodeId));
        if (podcast == null)
        {
            logger.LogError("Unable to find episode to reindex. Episode-id: '{episodeId}'.", episodeId);
            return;
        }

        var episode = podcast.Episodes.Where(e => e.Id == episodeId);
        if (episode.Count() > 1)
        {
            logger.LogError("Multiple episodes with Episode-id: '{episodeId}'.", episodeId);
            return;
        }

        var document = new PodcastEpisode(podcast, podcast.Episodes.Single(x => x.Id == episodeId))
            .ToEpisodeSearchRecord();

        var result =
            await searchClient.MergeOrUploadDocumentsAsync([document],
                new IndexDocumentsOptions { ThrowOnAnyError = true }, c);
    }

    public async Task IndexEpisodes(IEnumerable<Guid> episodeIds, CancellationToken c)
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
            var failures = result.Value.Results.Where(x => x.Succeeded == false);
            foreach (var failure in failures)
            {
                logger.LogError("Failed to index episode with key '{Key}': {ErrorMessage}", failure.Key,
                    failure.ErrorMessage);
            }

            if (failures.Any())
            {
                throw new RequestFailedException(result.GetRawResponse());
            }
        }
        else
        {
            logger.LogWarning("No documents to update in search-index");
        }
    }
}