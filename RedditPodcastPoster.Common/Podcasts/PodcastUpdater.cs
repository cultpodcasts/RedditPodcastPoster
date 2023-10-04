using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.EliminationTerms;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastUpdater : IPodcastUpdater
{
    private readonly IEpisodeProvider _episodeProvider;
    private readonly ILogger<PodcastUpdater> _logger;
    private readonly IPodcastFilter _podcastFilter;
    private readonly IEliminationTermsProvider _eliminationTermsProvider;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastServicesEpisodeEnricher _podcastServicesEpisodeEnricher;

    public PodcastUpdater(
        IPodcastRepository podcastRepository,
        IEpisodeProvider episodeProvider,
        IPodcastServicesEpisodeEnricher podcastServicesEpisodeEnricher,
        IPodcastFilter podcastFilter,
        IEliminationTermsProvider eliminationTermsProvider,
        ILogger<PodcastUpdater> logger
    )
    {
        _podcastRepository = podcastRepository;
        _episodeProvider = episodeProvider;
        _podcastServicesEpisodeEnricher = podcastServicesEpisodeEnricher;
        _podcastFilter = podcastFilter;
        _eliminationTermsProvider = eliminationTermsProvider;
        _logger = logger;
    }

    public async Task<IndexPodcastResult> Update(Podcast podcast, IndexingContext indexingContext)
    {
        var initialSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
        var initialSkipYouTube = indexingContext.SkipYouTubeUrlResolving;
        var newEpisodes = await _episodeProvider.GetEpisodes(
            podcast,
            indexingContext);
        var mergeResult = _podcastRepository.Merge(podcast, newEpisodes);
        var episodes = podcast.Episodes;
        if (indexingContext.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release >= indexingContext.ReleasedSince.Value).ToList();
        }

        await _podcastServicesEpisodeEnricher.EnrichEpisodes(podcast, episodes, indexingContext);
        var eliminationTerms = _eliminationTermsProvider.GetEliminationTerms();
        var filterResult = _podcastFilter.Filter(podcast, eliminationTerms.Terms);
        await _podcastRepository.Update(podcast);
        return new IndexPodcastResult(
            podcast,
            mergeResult,
            filterResult,
            initialSkipSpotify != indexingContext.SkipSpotifyUrlResolving,
            initialSkipYouTube != indexingContext.SkipYouTubeUrlResolving);
    }
}