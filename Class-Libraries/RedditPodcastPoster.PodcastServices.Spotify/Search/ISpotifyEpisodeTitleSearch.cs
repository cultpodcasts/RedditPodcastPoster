using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Search;

public interface ISpotifyEpisodeTitleSearch
{
    /// <summary>
    /// One episode-title search page, limited to the known show. Does not page a show catalogue.
    /// </summary>
    Task<IReadOnlyList<SimpleEpisode>> FindCandidates(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext,
        string market);
}
