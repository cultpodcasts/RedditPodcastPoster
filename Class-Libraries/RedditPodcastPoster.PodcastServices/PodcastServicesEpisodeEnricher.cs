using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Enrichers;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.YouTube.Enrichment;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

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
        IEnumerable<Episode> episodes,
        IList<Episode> newEpisodes,
        IndexingContext indexingContext
    )
    {
        var results = new List<EnrichmentResult>();
        foreach (var episode in newEpisodes)
        {
            var enrichmentContext = new EnrichmentContext();
            var enrichmentRequest = new EnrichmentRequest(podcast, episodes, episode);

            if (episode.Urls.Spotify == null || string.IsNullOrWhiteSpace(episode.SpotifyId))
            {
                await spotifyEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
            }

            if (episode.Urls.Apple == null || episode.AppleId == null || episode.AppleId == 0)
            {
                await appleEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
            }

            if (podcast.SkipEnrichingFromYouTube is null or false &&
                !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
                (episode.Urls.YouTube == null || string.IsNullOrWhiteSpace(episode.YouTubeId)))
            {
                await youTubeEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
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
                var delayedEpisodes = episodes
                    .Where(episode => podcastEpisodeFilter.IsRecentlyExpiredDelayedPublishing(podcast, episode))
                    .Where(delayedEpisode => !newEpisodes.Contains(delayedEpisode))
                    .Where(delayedEpisode => delayedEpisode.Urls.YouTube == null ||
                                             string.IsNullOrWhiteSpace(delayedEpisode.YouTubeId));
                foreach (var delayedEpisode in delayedEpisodes)
                {
                    var enrichmentContext = new EnrichmentContext();
                    var enrichmentRequest = new EnrichmentRequest(podcast, episodes, delayedEpisode);
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
