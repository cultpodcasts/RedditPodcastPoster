using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface ISpotifyEnrichmentSideEffect
{
    void OnFindComplete(Podcast podcast, bool isExpensiveQuery);
}
