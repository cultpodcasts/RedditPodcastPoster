using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

namespace RedditPodcastPoster.Discovery;

public class SpotifyEnricher(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    ILogger<ISpotifyEnricher> logger
) : ISpotifyEnricher
{
    public async Task Enrich(IEnumerable<EpisodeResult> results, IndexingContext indexingContext)
    {
        logger.LogInformation("{nameofSpotifyEnricher}.{nameofEnrich} initiated",
            nameof(SpotifyEnricher), nameof(Enrich));
        var enrichedCtr = 0;
        foreach (var episodeResult in results)
        {
            logger.LogInformation(
                "Enriching show-name '{episodeResultShowName}' episode-name '{episodeResultEpisodeName}'.",
                episodeResult.ShowName, episodeResult.EpisodeName);
            var episodeRequest = new FindSpotifyEpisodeRequest(
                string.Empty,
                episodeResult.ShowName,
                string.Empty,
                episodeResult.EpisodeName,
                episodeResult.Released,
                true);

            var spotifyResult = await spotifyEpisodeResolver.FindEpisode(
                episodeRequest, indexingContext);
            if (spotifyResult.FullEpisode != null)
            {
                enrichedCtr++;
                episodeResult.Urls.Spotify = spotifyResult.FullEpisode.GetUrl();
                episodeResult.EnrichedUrlFromSpotify = true;
                episodeResult.PodcastIds.Spotify = spotifyResult.FullEpisode.Show.Id;
                episodeResult.ImageUrl = spotifyResult.FullEpisode.GetBestImageUrl();
            }

            if (string.IsNullOrWhiteSpace(episodeResult.ShowDescription) && spotifyResult.FullEpisode != null)
            {
                episodeResult.ShowDescription = spotifyResult.FullEpisode.Show.Description;
            }
        }

        logger.LogInformation("{nameofEnrich} enriched '{enrichedCtr}' results.",
            nameof(Enrich), enrichedCtr);
    }
}