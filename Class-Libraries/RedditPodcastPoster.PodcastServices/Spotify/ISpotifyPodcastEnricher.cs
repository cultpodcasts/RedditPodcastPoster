using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifyPodcastEnricher
{
    Task<bool> AddIdAndUrls(Podcast podcast, IndexingContext indexingContext);
}