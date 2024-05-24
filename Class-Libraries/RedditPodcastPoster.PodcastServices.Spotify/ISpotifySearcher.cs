using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifySearcher
{
    Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}