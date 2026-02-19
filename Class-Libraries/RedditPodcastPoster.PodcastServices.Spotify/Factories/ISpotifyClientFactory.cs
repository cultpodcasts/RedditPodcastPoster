using RedditPodcastPoster.DependencyInjection;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Factories;

public interface ISpotifyClientFactory : IAsyncFactory<ISpotifyClient>
{
}