using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Index;

internal class IndexProcessor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IIndexer indexer,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<IndexProcessor> logger)
{
    public async Task Run(IndexRequest request)
    {
        if (request.ReindexSearch)
        {
            await ReindexSearchAsync(request);
            return;
        }

        DateTime? releasedSince = null;
        if (request.ReleasedSince > 0)
        {
            releasedSince = DateTimeExtensions.DaysAgo(request.ReleasedSince);
        }

        var indexingContext = new IndexingContext(releasedSince)
        {
            IndexSpotify = !request.SkipSpotifyIndexing,
            SkipExpensiveYouTubeQueries = request.SkipExpensiveYouTubeQueries,
            SkipPodcastDiscovery = request.SkipPodcastDiscovery,
            SkipExpensiveSpotifyQueries = request.SkipExpensiveSpotifyQueries,
            SkipYouTubeUrlResolving = request.SkipYouTubeUrlResolving,
            SkipSpotifyUrlResolving = request.SkipSpotifyUrlResolving
        };

        List<Guid> updatedEpisodeIds = [];
        if (request is { PodcastName: not null, UseSinglePodcastNameFlow: true })
        {
            var response = await indexer.Index(request.PodcastName, indexingContext);
            if (response.UpdatedEpisodes != null && response.UpdatedEpisodes.Any())
            {
                updatedEpisodeIds.AddRange(response.UpdatedEpisodes.Select(x => x.Episode.Id));
            }
        }
        else
        {
            var podcastIds = await ResolvePodcastIds(request);
            foreach (var podcastId in podcastIds)
            {
                var response = await indexer.Index(podcastId, indexingContext, request.ForceIndex);
                if (response.UpdatedEpisodes != null && response.UpdatedEpisodes.Any())
                {
                    updatedEpisodeIds.AddRange(response.UpdatedEpisodes.Select(x => x.Episode.Id));
                }
            }
        }

        if (!request.NoIndex && updatedEpisodeIds.Any())
        {
            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
    }

    private async Task ReindexSearchAsync(IndexRequest request)
    {
        if (request.NoIndex)
        {
            throw new InvalidOperationException("Cannot combine --reindex-search with --no-index.");
        }

        var podcastIds = await ResolvePodcastIds(request);
        if (podcastIds.Count == 0)
        {
            throw new InvalidOperationException(
                "No podcasts matched. Pass --podcast-id or --podcast-name with --reindex-search.");
        }

        var episodeIds = new List<Guid>();
        var removedCount = 0;
        foreach (var podcastId in podcastIds)
        {
            await foreach (var episode in episodeRepository.GetByPodcastId(podcastId))
            {
                episodeIds.Add(episode.Id);
                if (episode.Removed)
                {
                    removedCount++;
                }
            }
        }

        // IndexEpisodes uploads active docs and deletes Removed / podcast-removed ones.
        logger.LogInformation(
            "Reindexing {EpisodeCount} episode(s) across {PodcastCount} podcast(s) into Azure Search ({RemovedCount} Removed — will be deleted from the index).",
            episodeIds.Count,
            podcastIds.Count,
            removedCount);

        if (episodeIds.Count == 0)
        {
            return;
        }

        var result = await episodeSearchIndexerService.IndexEpisodes(episodeIds, CancellationToken.None);
        logger.LogInformation("Search reindex result: {Result}", result);
    }

    private async Task<List<Guid>> ResolvePodcastIds(IndexRequest request)
    {
        if (request.PodcastId.HasValue)
        {
            return [request.PodcastId.Value];
        }

        if (request.PodcastName != null)
        {
            var ids = await podcastRepository
                .GetAllBy(x => x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.Id)
                .ToListAsync();
            logger.LogInformation("Found {podcastIdsCount} podcasts.", ids.Count);
            return ids;
        }

        if (request.ReindexSearch)
        {
            return [];
        }

        return await podcastRepository.GetAll().Select(x => x.Id).ToListAsync();
    }
}
