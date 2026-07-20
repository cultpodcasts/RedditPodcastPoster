using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

public interface ISpotifyEpisodeResolver
{
    Task<FindEpisodeResponse> FindEpisode(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext,
        Func<SimpleEpisode, bool>? reducer = null);
}
