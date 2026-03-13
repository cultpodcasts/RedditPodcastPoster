using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify.Enrichers;

public interface ISpotifyPodcastEnricher
{
    Task<bool> AddIdAndUrls(Podcast podcast, IEnumerable<Episode> episodes, IndexingContext indexingContext);
}