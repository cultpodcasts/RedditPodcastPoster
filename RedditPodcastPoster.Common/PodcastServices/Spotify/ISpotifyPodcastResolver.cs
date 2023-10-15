namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyPodcastResolver
{
    Task<SpotifyPodcastWrapper?> FindPodcast(FindSpotifyPodcastRequest request, IndexingContext indexingContext);
}