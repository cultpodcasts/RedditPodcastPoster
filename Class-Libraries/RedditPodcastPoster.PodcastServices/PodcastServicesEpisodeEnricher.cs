using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.PodcastServices;

public class PodcastServicesEpisodeEnricher(
    IAppleEpisodeEnricher appleEpisodeEnricher,
    ISpotifyEpisodeEnricher spotifyEpisodeEnricher,
    IYouTubeEpisodeEnricher youTubeEpisodeEnricher,
    IPodcastEpisodeFilter podcastEpisodeFilter,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PodcastServicesEpisodeEnricher> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IPodcastServicesEpisodeEnricher
{
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
                        await spotifyEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
                        break;
                    case Service.Apple
                        when episode.Urls.Apple == null || episode.AppleId == null || episode.AppleId == 0:
                        await appleEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
                        break;
                    case Service.YouTube
                        when podcast.SkipEnrichingFromYouTube is null or false &&
                             !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) && (episode.Urls.YouTube == null ||
                                 string.IsNullOrWhiteSpace(episode.YouTubeId)):
                        await youTubeEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
                        break;
                }
            }

            if (enrichmentContext.Updated)
            {
                results.Add(new EnrichmentResult(podcast, episode, enrichmentContext));
            }
        }

        if (podcast.SkipEnrichingFromYouTube == null &&
            !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            if (podcast.YouTubePublishingDelay() > TimeSpan.Zero)
            {
                var delayedEpisodes = podcast.Episodes
                    .Where(episode => podcastEpisodeFilter.IsRecentlyExpiredDelayedPublishing(podcast, episode))
                    .Where(delayedEpisode => !newEpisodes.Contains(delayedEpisode))
                    .Where(delayedEpisode => delayedEpisode.Urls.YouTube == null ||
                                             string.IsNullOrWhiteSpace(delayedEpisode.YouTubeId));
                foreach (var delayedEpisode in delayedEpisodes)
                {
                    var enrichmentContext = new EnrichmentContext();
                    var enrichmentRequest = new EnrichmentRequest(podcast, delayedEpisode);
                    await youTubeEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
                    if (enrichmentContext.Updated)
                    {
                        results.Add(new EnrichmentResult(podcast, delayedEpisode, enrichmentContext));
                    }
                }
            }
        }

        return new EnrichmentResults(results);
    }
}