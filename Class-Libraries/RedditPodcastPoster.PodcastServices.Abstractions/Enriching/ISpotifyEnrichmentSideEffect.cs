using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Enriching;

public interface ISpotifyEnrichmentSideEffect
{
    /// <param name="isExpensiveQuery">
    /// Catalogue-order probe: true = oldest-first, false = newest-first, null = inconclusive (do not change flag).
    /// </param>
    void OnFindComplete(Podcast podcast, bool? isExpensiveQuery);
}
