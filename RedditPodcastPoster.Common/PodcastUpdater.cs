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

    private void MergeEnrichedProperties(Episode existingEpisode, Episode episodeToMerge)
    {
        existingEpisode.Urls.Spotify ??= episodeToMerge.Urls.Spotify;
        existingEpisode.Urls.YouTube ??= episodeToMerge.Urls.YouTube;
        if (string.IsNullOrWhiteSpace(existingEpisode.SpotifyId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.SpotifyId))
        {
            existingEpisode.SpotifyId = episodeToMerge.SpotifyId;
        }

        if (string.IsNullOrWhiteSpace(existingEpisode.YouTubeId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.YouTubeId))
        {
            existingEpisode.YouTubeId = episodeToMerge.YouTubeId;
        }
    }
}