using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public class PodcastServicesEpisodeEnricher : IPodcastServicesEpisodeEnricher
{
    private readonly IAppleEpisodeEnricher _appleEpisodeEnricher;
    private readonly ILogger<PodcastServicesEpisodeEnricher> _logger;
    private readonly ISpotifyEpisodeEnricher _spotifyEpisodeEnricher;
    private readonly IYouTubeEpisodeEnricher _youTubeEpisodeEnricher;

    public PodcastServicesEpisodeEnricher(
        IAppleEpisodeEnricher appleEpisodeEnricher,
        ISpotifyEpisodeEnricher spotifyEpisodeEnricher,
        IYouTubeEpisodeEnricher youTubeEpisodeEnricher,
        ILogger<PodcastServicesEpisodeEnricher> logger)
    {
        _appleEpisodeEnricher = appleEpisodeEnricher;
        _spotifyEpisodeEnricher = spotifyEpisodeEnricher;
        _youTubeEpisodeEnricher = youTubeEpisodeEnricher;
        _logger = logger;
    }

    public async Task<EnrichmentResults> EnrichEpisodes(
        Podcast podcast,
        IList<Episode> newEpisodes,
        IndexingContext indexingContext
    )
    {
        var results = new List<EnrichmentResult>();
        foreach (var episode in newEpisodes)
        {
            var enrichmentContext = new EnrichmentContext();
            var enrichmentRequest = new EnrichmentRequest(podcast, episode);
            foreach (Service service in Enum.GetValues(typeof(Service)))
            {
                switch (service)
                {
                    case Service.Spotify
                        when episode.Urls.Spotify == null || string.IsNullOrWhiteSpace(episode.SpotifyId):
                        await _spotifyEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
                        break;
                    case Service.Apple
                        when episode.Urls.Apple == null || episode.AppleId == null || episode.AppleId == 0:
                        await _appleEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
                        break;
                    case Service.YouTube when !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
                                              (episode.Urls.YouTube == null ||
                                               string.IsNullOrWhiteSpace(episode.YouTubeId)):
                        await _youTubeEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
                        break;
                }
            }

            if (enrichmentContext.Updated)
            {
                results.Add(new EnrichmentResult(podcast, episode, enrichmentContext));
            }
        }

        return new EnrichmentResults(results);
    }
}