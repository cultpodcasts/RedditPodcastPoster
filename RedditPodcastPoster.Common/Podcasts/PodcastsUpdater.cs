using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Models;

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
        IEnumerable<Podcast> podcasts = await _podcastRepository.GetAll().ToListAsync();
        _logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing Starting.");
        foreach (var podcast in podcasts)
        {
            IndexPodcastResult? result= null;
            if (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
            {
                result = await _podcastUpdater.Update(podcast, indexingContext);
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