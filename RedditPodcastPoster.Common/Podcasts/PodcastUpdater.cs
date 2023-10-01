using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.EliminationTerms;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastUpdater : IPodcastUpdater
{
    private readonly IEliminationTermsRepository _eliminationTermsRepository;
    private readonly IEpisodeProvider _episodeProvider;
    private readonly ILogger<PodcastUpdater> _logger;
    private readonly IPodcastFilter _podcastFilter;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastServicesEpisodeEnricher _podcastServicesEpisodeEnricher;

    public PodcastUpdater(
        IPodcastRepository podcastRepository,
        IEpisodeProvider episodeProvider,
        IPodcastServicesEpisodeEnricher podcastServicesEpisodeEnricher,
        IPodcastFilter podcastFilter,
        IEliminationTermsRepository eliminationTermsRepository,
        ILogger<PodcastUpdater> logger
    )
    {
        _podcastRepository = podcastRepository;
        _episodeProvider = episodeProvider;
        _podcastServicesEpisodeEnricher = podcastServicesEpisodeEnricher;
        _podcastFilter = podcastFilter;
        _eliminationTermsRepository = eliminationTermsRepository;
        _logger = logger;
    }

    public async Task<IndexPodcastResult> Update(Podcast podcast, IndexOptions indexOptions)
    {
        var initialSkipSpotify = indexOptions.SkipSpotifyUrlResolving;
        var initialSkipYouTube = indexOptions.SkipYouTubeUrlResolving;
        var newEpisodes = await _episodeProvider.GetEpisodes(
            podcast,
            indexOptions);
        var mergeResult = _podcastRepository.Merge(podcast, newEpisodes);
        var episodes = podcast.Episodes;
        if (indexOptions.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release >= indexOptions.ReleasedSince.Value).ToList();
        }

        await _podcastServicesEpisodeEnricher.EnrichEpisodes(podcast, episodes, indexOptions);
        var eliminationTerms = await _eliminationTermsRepository.Get();
        var filterResult = _podcastFilter.Filter(podcast, eliminationTerms.Terms);
        await _podcastRepository.Update(podcast);
        return new IndexPodcastResult(
            podcast,
            mergeResult, 
            filterResult,
            initialSkipSpotify == indexOptions.SkipSpotifyUrlResolving,
            initialSkipYouTube == indexOptions.SkipYouTubeUrlResolving);
    }
}