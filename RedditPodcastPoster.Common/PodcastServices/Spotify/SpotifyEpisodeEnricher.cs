using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyEpisodeEnricher : ISpotifyEpisodeEnricher
{
    private readonly ISpotifyEpisodeResolver _spotifyEpisodeResolver;
    private readonly ILogger<SpotifyEpisodeEnricher> _logger;

    public SpotifyEpisodeEnricher(
        ISpotifyEpisodeResolver spotifyEpisodeResolver,
        ILogger<SpotifyEpisodeEnricher> logger)
    {
        _spotifyEpisodeResolver = spotifyEpisodeResolver;
        _logger = logger;
    }

    public async Task Enrich(EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext enrichmentContext)
    {
        var spotifyEpisode =
            await _spotifyEpisodeResolver.FindEpisode(
                FindSpotifyEpisodeRequestFactory.Create(request.Podcast, request.Episode), indexingContext);
        if (spotifyEpisode != null)
        {
            _logger.LogInformation(
                $"{nameof(Enrich)} Found matching Spotify episode: '{spotifyEpisode.Id}' with title '{spotifyEpisode.Name}' and release-date '{spotifyEpisode.ReleaseDate}'.");
            request.Episode.SpotifyId = spotifyEpisode.Id;
            var url = spotifyEpisode.GetUrl();
            request.Episode.Urls.Spotify = url;
            enrichmentContext.Spotify = url;
        }
    }
}