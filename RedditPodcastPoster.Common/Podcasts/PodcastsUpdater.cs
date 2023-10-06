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

    public async Task<IndexPodcastsResult> UpdatePodcasts(IndexingContext indexingContext)
    {
        var results = new List<IndexPodcastResult>();
        _logger.LogInformation($"{nameof(UpdatePodcasts)} Retrieving podcasts.");
        IEnumerable<Podcast> podcasts = await _podcastRepository.GetAll().ToListAsync();
        _logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing Starting.");
        foreach (var podcast in podcasts)
        {
            _flushableCaches.Flush();
            if (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
            {
                results.Add(await _podcastUpdater.Update(podcast, indexingContext));
            }
        }

        _logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing complete.");
        return new IndexPodcastsResult(results);
    }
}