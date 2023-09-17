using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastUpdater : IPodcastUpdater
{
    private readonly IEpisodeProvider _episodeProvider;
    private readonly ILogger<PodcastUpdater> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastServicesEpisodeEnricher _podcastServicesEpisodeEnricher;

    public PodcastUpdater(
        IPodcastRepository podcastRepository,
        IEpisodeProvider episodeProvider,
        IPodcastServicesEpisodeEnricher podcastServicesEpisodeEnricher,
        ILogger<PodcastUpdater> logger
    )
    {
        _podcastRepository = podcastRepository;
        _episodeProvider = episodeProvider;
        _podcastServicesEpisodeEnricher = podcastServicesEpisodeEnricher;
        _logger = logger;
    }

    public async Task Update(Podcast podcast, IndexOptions indexOptions)
    {
        var newEpisodes =
            await _episodeProvider.GetEpisodes(
                podcast,
                indexOptions.ReleasedSince,
                indexOptions.SkipYouTubeUrlResolving);
        _podcastRepository.Merge(podcast, newEpisodes);

        var episodes = podcast.Episodes;

        if (indexOptions.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release > indexOptions.ReleasedSince.Value).ToList();
        }

        await _podcastServicesEpisodeEnricher.EnrichEpisodes(
            podcast,
            episodes,
            indexOptions);
    }
}