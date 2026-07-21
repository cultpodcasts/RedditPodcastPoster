using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Enriching;

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
