using Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Api.Services.Podcasts;

public class PodcastIndexService(
    IIndexer indexer,
    IEpisodeSearchIndexerService searchIndexerService,
    IOptions<IndexerOptions> indexerOptions,
    ILogger<PodcastIndexService> logger) : IPodcastIndexService
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public async Task<PodcastIndexResult> IndexAsync(
        string podcastName,
        CancellationToken c,
        bool backlog = false)
    {
        try
        {
            logger.LogInformation(
                "{method}: Index podcast '{podcastName}' (backlog={backlog}).",
                nameof(IndexAsync),
                podcastName,
                backlog);
            return await IndexPodcast(c, () => indexer.Index(podcastName, CreateIndexingContext(backlog)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to index-podcast.", nameof(IndexAsync));
            return new PodcastIndexResult(PodcastIndexStatus.Failed);
        }
    }

    private IndexingContext CreateIndexingContext(bool backlog) =>
        CuratorIndexingContext.Create(_indexerOptions, backlog);

    private async Task<PodcastIndexResult> IndexPodcast(
        CancellationToken c,
        Func<Task<IndexResponse>> index)
    {
        try
        {
            var response = await index();
            EntitySearchIndexerResponse? searchIndexer = null;
            if (response.IndexStatus == IndexStatus.Performed)
            {
                var episodes = response.UpdatedEpisodes != null
                    ? response.UpdatedEpisodes.Select(x => x.Episode.Id)
                    : [];
                searchIndexer = await searchIndexerService.IndexEpisodes(episodes, c);
            }

            var status = response.IndexStatus switch
            {
                IndexStatus.NotFound => PodcastIndexStatus.NotFound,
                IndexStatus.Performed => PodcastIndexStatus.Ok,
                IndexStatus.AlreadyRunning => PodcastIndexStatus.AlreadyRunning,
                IndexStatus.TimedOut => PodcastIndexStatus.TimedOut,
                _ => PodcastIndexStatus.BadRequest
            };

            if (status == PodcastIndexStatus.NotFound)
            {
                logger.LogWarning("{method}: Podcast not found during index request.", nameof(IndexPodcast));
            }

            return new PodcastIndexResult(status, response, searchIndexer);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Index request failed.", nameof(IndexPodcast));
            return new PodcastIndexResult(PodcastIndexStatus.Failed);
        }
    }
}
