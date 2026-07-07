using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify.Enrichers;

public sealed class SpotifyExpensiveQuerySideEffect : ISpotifyEnrichmentSideEffect
{
    public void OnFindComplete(Podcast podcast, bool isExpensiveQuery)
    {
        if (isExpensiveQuery)
        {
            podcast.SpotifyEpisodesQueryIsExpensive = true;
        }
    }
}
