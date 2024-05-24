using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;

namespace RedditPodcastPoster.Discovery;

public class SpotifyEnricher(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    ILogger<ISpotifyEnricher> logger
) : ISpotifyEnricher
{
    public async Task Enrich(IEnumerable<EpisodeResult> results, IndexingContext indexingContext)
    {
        logger.LogInformation($"{nameof(Enrich)} initiated");
        var enrichedCtr = 0;
        foreach (var episodeResult in results)
        {
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
                var image = spotifyResult.FullEpisode.Images.MaxBy(x => x.Height);

                episodeResult.Url = spotifyResult.FullEpisode.GetUrl();
                episodeResult.DiscoverService = DiscoverService.Spotify;
                episodeResult.ServicePodcastId = spotifyResult.FullEpisode.Show.Id;
                episodeResult.ImageUrl = image != null ? new Uri(image.Url) : null;
            }
        }
        logger.LogInformation($"{nameof(Enrich)} enriched '{enrichedCtr}' results.");

    }
}