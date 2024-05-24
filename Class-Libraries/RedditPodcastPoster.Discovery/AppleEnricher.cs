using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;

namespace RedditPodcastPoster.Discovery;

public class AppleEnricher(
    IAppleEpisodeResolver appleEpisodeResolver,
    ILogger<ISpotifyEnricher> logger
) : IAppleEnricher
{
    public async Task Enrich(IEnumerable<EpisodeResult> results, IndexingContext indexingContext)
    {
        logger.LogInformation($"{nameof(Enrich)} initiated");
        var enrichedCtr = 0;

        foreach (var episodeResult in results)
        {
            var episodeRequest = new FindAppleEpisodeRequest(
                null,
                episodeResult.ShowName,
                null,
                episodeResult.EpisodeName,
                episodeResult.Released,
                null, episodeResult.Length);

            var appleResult = await appleEpisodeResolver.FindEpisode(
                episodeRequest, indexingContext);
            if (appleResult != null)
            {
                enrichedCtr++;
                episodeResult.Released = appleResult.Release;
            }
        }

        logger.LogInformation($"{nameof(Enrich)} enriched '{enrichedCtr}' results.");
    }
}