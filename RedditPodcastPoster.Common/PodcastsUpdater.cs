using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common;

public class PodcastsUpdater : IPodcastsUpdater
{
    private readonly ILogger<PodcastsUpdater> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastUpdater _podcastUpdater;

    public PodcastsUpdater(
        IPodcastUpdater podcastUpdater,
        IPodcastRepository podcastRepository,
        ILogger<PodcastsUpdater> logger
    )
    {
        _podcastUpdater = podcastUpdater;
        _podcastRepository = podcastRepository;
        _logger = logger;
    }

    public async Task UpdatePodcasts(IndexOptions indexOptions)
    {
        IEnumerable<Podcast> podcasts = await _podcastRepository.GetAll().ToListAsync();
        foreach (var podcast in podcasts.Where(x=>x.IndexAllEpisodes))
        {
            await _podcastUpdater.Update(podcast, indexOptions);

            await _podcastRepository.Update(podcast);
        }
    }
}