using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

public interface ISpotifyPodcastResolver
{
    Task<SpotifyPodcastWrapper?> FindPodcast(FindSpotifyPodcastRequest request, IndexingContext indexingContext);
}