using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyEpisodeEnricher(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    ILogger<SpotifyEpisodeEnricher> logger)
    : ISpotifyEpisodeEnricher
{
    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        var findSpotifyEpisodeRequest = FindSpotifyEpisodeRequestFactory.Create(request.Podcast, request.Episode);
        var findEpisodeResult = await spotifyEpisodeResolver.FindEpisode(findSpotifyEpisodeRequest, indexingContext);
        if (findEpisodeResult.FullEpisode != null)
        {
            logger.LogInformation(
                $"{nameof(Enrich)} Found matching Spotify episode: '{findEpisodeResult.FullEpisode.Id}' with title '{findEpisodeResult.FullEpisode.Name}' and release-date '{findEpisodeResult.FullEpisode.ReleaseDate}'.");
            request.Episode.SpotifyId = findEpisodeResult.FullEpisode.Id;
            var url = findEpisodeResult.FullEpisode.GetUrl();
            request.Episode.Urls.Spotify = url;
            enrichmentContext.Spotify = url;
            var description = findEpisodeResult.FullEpisode.GetDescription();
            if (string.IsNullOrWhiteSpace(request.Episode.Description) &&
                !string.IsNullOrWhiteSpace(description))
            {
                request.Episode.Description = description;
            }
        }

        if (findEpisodeResult.IsExpensiveQuery)
        {
            request.Podcast.SpotifyEpisodesQueryIsExpensive = true;
        }
    }
}