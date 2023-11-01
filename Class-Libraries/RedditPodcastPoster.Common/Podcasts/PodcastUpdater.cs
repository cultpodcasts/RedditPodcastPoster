using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastUpdater : IPodcastUpdater
{
    private readonly IEliminationTermsProvider _eliminationTermsProvider;
    private readonly IEpisodeProvider _episodeProvider;
    private readonly ILogger<PodcastUpdater> _logger;
    private readonly IPodcastFilter _podcastFilter;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastServicesEpisodeEnricher _podcastServicesEpisodeEnricher;
    private readonly PostingCriteria _postingCriteria;

    public PodcastUpdater(
        IPodcastRepository podcastRepository,
        IEpisodeProvider episodeProvider,
        IPodcastServicesEpisodeEnricher podcastServicesEpisodeEnricher,
        IPodcastFilter podcastFilter,
        IEliminationTermsProvider eliminationTermsProvider,
        IOptions<PostingCriteria> postingCriteria,
        ILogger<PodcastUpdater> logger
    )
    {
        _podcastRepository = podcastRepository;
        _episodeProvider = episodeProvider;
        _podcastServicesEpisodeEnricher = podcastServicesEpisodeEnricher;
        _podcastFilter = podcastFilter;
        _eliminationTermsProvider = eliminationTermsProvider;
        _postingCriteria = postingCriteria.Value;
        _logger = logger;
    }

    public async Task<IndexPodcastResult> Update(Podcast podcast, IndexingContext indexingContext)
    {
        var initialSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
        var initialSkipYouTube = indexingContext.SkipYouTubeUrlResolving;
        var knownYouTubeExpensiveQuery = podcast.HasExpensiveYouTubePlaylistQuery();
        var knownSpotifyExpensiveQuery = podcast.HasExpensiveSpotifyEpisodesQuery();
        var newEpisodes = await _episodeProvider.GetEpisodes(
            podcast,
            indexingContext);

        foreach (var newEpisode in newEpisodes)
        {
            newEpisode.Ignored = newEpisode.Length < _postingCriteria.MinimumDuration;
        }

        if (indexingContext.SkipShortEpisodes)
        {
            EliminateShortEpisodes(newEpisodes);
        }

        var mergeResult = _podcastRepository.Merge(podcast, newEpisodes);
        var episodes = podcast.Episodes;
        if (indexingContext.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release >= indexingContext.ReleasedSince.Value).ToList();
        }

        var enrichmentResult = await _podcastServicesEpisodeEnricher.EnrichEpisodes(podcast, episodes, indexingContext);
        var eliminationTerms = _eliminationTermsProvider.GetEliminationTerms();
        var filterResult = _podcastFilter.Filter(podcast, eliminationTerms.Terms);

        var discoveredYouTubeExpensiveQuery = !knownYouTubeExpensiveQuery && podcast.HasExpensiveYouTubePlaylistQuery();
        if (discoveredYouTubeExpensiveQuery)
        {
            _logger.LogInformation(
                $"Expensive YouTube Query found processing '{podcast.Name}' with id '{podcast.Id}' and youtube-channel-id '{podcast.YouTubeChannelId}'.");
        }

        var discoveredSpotifyExpensiveQuery = !knownSpotifyExpensiveQuery && podcast.HasExpensiveSpotifyEpisodesQuery();
        if (discoveredSpotifyExpensiveQuery)
        {
            _logger.LogInformation(
                $"Expensive Spotify Query found processing '{podcast.Name}' with id '{podcast.Id}' and spotify-id '{podcast.SpotifyId}'.");
        }

        if (mergeResult.MergedEpisodes.Any() || mergeResult.AddedEpisodes.Any() ||
            filterResult.FilteredEpisodes.Any() || enrichmentResult.UpdatedEpisodes.Any() ||
            discoveredYouTubeExpensiveQuery || discoveredSpotifyExpensiveQuery)
        {
            await _podcastRepository.Update(podcast);
        }

        return new IndexPodcastResult(
            podcast,
            mergeResult,
            filterResult,
            enrichmentResult,
            initialSkipSpotify != indexingContext.SkipSpotifyUrlResolving,
            initialSkipYouTube != indexingContext.SkipYouTubeUrlResolving);
    }

    private void EliminateShortEpisodes(IList<Episode> episodes)
    {
        List<Episode> shortEpisodes = new List<Episode>();

        foreach (var newEpisode in episodes)
        {
            if (newEpisode.Ignored)
            {
                shortEpisodes.Add(newEpisode);
            }
        }

        foreach (var shortEpisode in shortEpisodes)
        {
            episodes.Remove(shortEpisode);
        }
    }
}