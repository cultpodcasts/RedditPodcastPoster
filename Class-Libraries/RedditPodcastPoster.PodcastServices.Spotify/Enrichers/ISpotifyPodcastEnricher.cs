using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify.Enrichers;

public interface ISpotifyPodcastEnricher
{
    Task<bool> AddIdAndUrls(Podcast podcast, IndexingContext indexingContext);
}