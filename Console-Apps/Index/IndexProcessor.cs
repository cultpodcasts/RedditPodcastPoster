using Index;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace IndexPodcast;

internal class IndexProcessor
{
    private readonly ILogger<IndexProcessor> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastUpdater _podcastUpdater;

    public IndexProcessor(
        IPodcastRepository podcastRepository,
        IPodcastUpdater podcastUpdater,
        ILogger<IndexProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _podcastUpdater = podcastUpdater;
        _logger = logger;
    }

    public async Task Run(IndexRequest request)
    {
        DateTime? releasedSince = null;
        if (request.ReleasedSince > 0)
        {
            releasedSince = DateTimeHelper.DaysAgo(request.ReleasedSince);
        }

        var indexingContext = new IndexingContext(releasedSince)
        {
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
        else
        {
            podcastIds = await _podcastRepository.GetAllIds();
        }

        foreach (var podcastId in podcastIds)
        {
            var podcast = await _podcastRepository.GetPodcast(podcastId);
            await _podcastUpdater.Update(podcast, indexingContext);
        }
    }
}