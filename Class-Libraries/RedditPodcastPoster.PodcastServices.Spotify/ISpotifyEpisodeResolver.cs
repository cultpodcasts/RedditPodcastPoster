using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifyEpisodeResolver
{
    Task<FindEpisodeResponse> FindEpisode(FindSpotifyEpisodeRequest request, IndexingContext indexingContext);
}