using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyEpisodeEnricher : ISpotifyEpisodeEnricher
{
    private readonly ILogger<SpotifyEpisodeEnricher> _logger;
    private readonly ISpotifyEpisodeResolver _spotifyEpisodeResolver;

    public SpotifyEpisodeEnricher(
        ISpotifyEpisodeResolver spotifyEpisodeResolver,
        ILogger<SpotifyEpisodeEnricher> logger)
    {
        _spotifyEpisodeResolver = spotifyEpisodeResolver;
        _logger = logger;
    }

    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        var findSpotifyEpisodeRequest = FindSpotifyEpisodeRequestFactory.Create(request.Podcast, request.Episode);
        var findEpisodeResult = await _spotifyEpisodeResolver.FindEpisode(findSpotifyEpisodeRequest, indexingContext);
        if (findEpisodeResult.FullEpisode != null)
        {
            _logger.LogInformation(
                $"{nameof(Enrich)} Found matching Spotify episode: '{findEpisodeResult.FullEpisode.Id}' with title '{findEpisodeResult.FullEpisode.Name}' and release-date '{findEpisodeResult.FullEpisode.ReleaseDate}'.");
            request.Episode.SpotifyId = findEpisodeResult.FullEpisode.Id;
            var url = findEpisodeResult.FullEpisode.GetUrl();
            request.Episode.Urls.Spotify = url;
            enrichmentContext.Spotify = url;
        }

        if (findEpisodeResult.IsExpensiveQuery)
        {
            request.Podcast.SpotifyEpisodesQueryIsExpensive = true;
        }
    }
}