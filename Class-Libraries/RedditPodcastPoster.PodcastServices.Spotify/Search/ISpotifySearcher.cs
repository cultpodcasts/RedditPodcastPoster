using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Search;

public interface ISpotifySearcher
{
    Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext);
}
