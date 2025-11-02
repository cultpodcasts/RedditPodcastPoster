using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Indexing;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Index;

internal class IndexProcessor(
    IPodcastRepository podcastRepository,
    IIndexer indexer,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<IndexProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task Run(IndexRequest request)
    {
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

        List<Guid> updatedEpisodeIds = new();
        if (request is { PodcastName: not null, UseSinglePodcastNameFlow: true })
        {
            var response = await indexer.Index(request.PodcastName, indexingContext);
            if (response.UpdatedEpisodes != null && response.UpdatedEpisodes.Any())
            {
                updatedEpisodeIds.AddRange(response.UpdatedEpisodes.Select(x => x.EpisodeId));
            }
        }
        else
        {
            IEnumerable<Guid> podcastIds;
            if (request.PodcastId.HasValue)
            {
                podcastIds = [request.PodcastId.Value];
            }
            else if (request.PodcastName != null)
            {
                podcastIds = await podcastRepository.GetAllBy(x =>
                        x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase),
                    x => x.Id).ToListAsync();
                logger.LogInformation("Found {podcastIdsCount} podcasts.", podcastIds.Count());
            }
            else
            {
                podcastIds = await podcastRepository.GetAllIds().ToArrayAsync();
            }

            foreach (var podcastId in podcastIds)
            {
                var response = await indexer.Index(podcastId, indexingContext);
                if (response.UpdatedEpisodes != null && response.UpdatedEpisodes.Any())
                {
                    updatedEpisodeIds.AddRange(response.UpdatedEpisodes.Select(x => x.EpisodeId));
                }
            }
        }


        if (!request.NoIndex && updatedEpisodeIds.Any())
        {
            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
    }
}