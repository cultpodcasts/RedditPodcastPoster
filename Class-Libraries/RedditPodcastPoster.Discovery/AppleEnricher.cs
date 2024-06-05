using iTunesSearch.Library.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;

namespace RedditPodcastPoster.Discovery;

public class AppleEnricher(
    IAppleEpisodeResolver appleEpisodeResolver,
    IApplePodcastResolver applePodcastResolver,
    ILogger<ISpotifyEnricher> logger
) : IAppleEnricher
{
    public async Task Enrich(IList<EpisodeResult> results, IndexingContext indexingContext)
    {
        logger.LogInformation($"{nameof(Enrich)} initiated");
        var enrichedCtr = 0;

        foreach (var episodeResult in results)
        {
            Podcast? podcast;
            if (episodeResult.ITunesPodcastId != null)
            {
                podcast = await applePodcastResolver.FindPodcast(
                    new FindApplePodcastRequest(episodeResult.ITunesPodcastId, episodeResult.ShowName, string.Empty));
            }
            else
            {
                podcast = await applePodcastResolver.FindPodcast(new FindApplePodcastRequest(
                    null,
                    episodeResult.ShowName,
                    string.Empty));
            }

            var episodeRequest = new FindAppleEpisodeRequest(
                podcast?.Id,
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
                episodeResult.Urls.Apple = appleResult.Url;
                episodeResult.PodcastIds.Apple ??= episodeResult.ITunesPodcastId ??
                                                   podcast?.Id ?? AppleIdResolver.GetPodcastId(appleResult.Url);
                episodeResult.EnrichedTimeFromApple = true;
            }
        }

        logger.LogInformation($"{nameof(Enrich)} enriched '{enrichedCtr}' results.");
    }
}