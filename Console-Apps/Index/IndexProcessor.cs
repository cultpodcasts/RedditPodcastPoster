using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Indexing;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Search;

namespace Index;

internal class IndexProcessor(
    IPodcastRepository podcastRepository,
    IIndexer indexer,
    ISearchIndexerService searchIndexerService,
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

        IEnumerable<Guid> podcastIds;
        if (request.PodcastId.HasValue)
        {
            podcastIds = new[] {request.PodcastId.Value};
        }
        else if (request.PodcastName != null)
        {
            podcastIds = await podcastRepository.GetAllBy(x =>
                    x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase),
                x => x.Id).ToListAsync();
            logger.LogInformation($"Found {podcastIds.Count()} podcasts.");
        }
        else
        {
            podcastIds = await podcastRepository.GetAllIds().ToArrayAsync();
        }

        foreach (var podcastId in podcastIds)
        {
            await indexer.Index(podcastId, indexingContext);
        }

        if (!request.NoIndex)
        {
            await searchIndexerService.RunIndexer();
        }
    }
}