using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Enriching;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Enrichers;

public sealed class SpotifyExpensiveQuerySideEffect(ILogger<SpotifyExpensiveQuerySideEffect> logger)
    : ISpotifyEnrichmentSideEffect
{
    public void OnFindComplete(Podcast podcast, bool? isExpensiveQuery)
    {
        // Enrichment always receives a probe that already required a conclusive sample size, or null.
        SpotifyExpensiveQueryFlag.Apply(
            podcast,
            isExpensiveQuery,
            isExpensiveQuery.HasValue ? SpotifyExpensiveQueryFlag.MinimumOrderSampleSize : 0,
            logger);
    }
}
