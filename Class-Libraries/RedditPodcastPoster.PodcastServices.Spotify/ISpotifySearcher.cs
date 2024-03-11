using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifySearcher
{
    Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}