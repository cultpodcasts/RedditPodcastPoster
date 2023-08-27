using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common;

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

    public async Task Update(Podcast podcast, DateTime? releasedSince, bool skipYouTubeUrlResolving)
    {
        var newEpisodes =
            await _episodeProvider.GetEpisodes(
                podcast,
                releasedSince,
                skipYouTubeUrlResolving) ??
            new List<Episode>();
        await _podcastRepository.Merge(podcast, newEpisodes, MergeEnrichedProperties);

        var episodes = podcast.Episodes;

        if (releasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release > releasedSince.Value).ToList();
        }

        await _podcastServicesEpisodeEnricher.EnrichEpisodes(
            podcast,
            episodes,
            releasedSince,
            skipYouTubeUrlResolving);
    }
}