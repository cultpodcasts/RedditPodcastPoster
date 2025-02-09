using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Factories;

public interface ISpotifyClientConfigFactory
{
    Task<SpotifyClientConfig> Create();
}