using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

public interface ISpotifyEpisodeResolver
{
    Task<FindEpisodeResponse> FindEpisode(FindSpotifyEpisodeRequest request, IndexingContext indexingContext);
}