using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify.Search;

public interface ISpotifySearcher
{
    Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}