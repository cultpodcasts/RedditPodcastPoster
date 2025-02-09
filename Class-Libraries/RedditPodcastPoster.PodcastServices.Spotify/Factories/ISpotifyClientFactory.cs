using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Factories;

public interface ISpotifyClientFactory
{
    Task<ISpotifyClient> Create();
}