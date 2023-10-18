using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifyClientFactory
{
    Task<ISpotifyClient> Create();
}