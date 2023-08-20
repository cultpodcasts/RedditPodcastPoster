using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyClientFactory
{
    Task<ISpotifyClient> Create();
}