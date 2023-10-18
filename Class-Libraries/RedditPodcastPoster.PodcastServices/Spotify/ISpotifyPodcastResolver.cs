using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifyPodcastResolver
{
    Task<SpotifyPodcastWrapper?> FindPodcast(FindSpotifyPodcastRequest request, IndexingContext indexingContext);
}