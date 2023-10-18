using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastsUpdater : IPodcastsUpdater
{
    private readonly IFlushable _flushableCaches;
    private readonly ILogger<PodcastsUpdater> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastUpdater _podcastUpdater;

    public PodcastsUpdater(
        IPodcastUpdater podcastUpdater,
        IPodcastRepository podcastRepository,
        IFlushable flushableCaches,
        ILogger<PodcastsUpdater> logger
    )
    {
        _podcastUpdater = podcastUpdater;
        _podcastRepository = podcastRepository;
        _flushableCaches = flushableCaches;
        _logger = logger;
    }

    public async Task<bool> UpdatePodcasts(IndexingContext indexingContext)
    {
        var success = true;
        _logger.LogInformation($"{nameof(UpdatePodcasts)} Retrieving podcasts.");
        var podcastIds = await _podcastRepository.GetAllIds();
        _logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing Starting.");
        foreach (var podcastId in podcastIds)
        {
            var podcast = await _podcastRepository.GetPodcast(podcastId);
            if (podcast != null &&
                (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex)))
            {
                var result = await _podcastUpdater.Update(podcast, indexingContext);
                var resultReport = result.ToString();
                if (!result.Success)
                {
                    _logger.LogError(resultReport);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(resultReport))
                    {
                        _logger.LogInformation(result.ToString());
                    }
                }

                success &= result.Success;
                _flushableCaches.Flush();
            }
        }

        _logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing complete.");
        return success;
    }
}