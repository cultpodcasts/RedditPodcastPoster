using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

public interface ISpotifyPodcastResolver
{
    Task<SpotifyPodcastWrapper?> FindPodcast(FindSpotifyPodcastRequest request, IndexingContext indexingContext);
}
