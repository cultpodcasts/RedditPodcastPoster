using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Enriching;

public interface ISpotifyEnrichmentSideEffect
{
    void OnFindComplete(Podcast podcast, bool isExpensiveQuery);
}
