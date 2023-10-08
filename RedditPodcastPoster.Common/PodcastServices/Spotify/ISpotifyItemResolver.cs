using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyItemResolver
{
    Task<FullEpisode?> FindEpisode(FindSpotifyEpisodeRequest request, IndexingContext indexingContext);
    Task<SpotifyPodcastWrapper?> FindPodcast(FindSpotifyPodcastRequest request, IndexingContext indexingContext);
    Task<IEnumerable<SimpleEpisode>?> GetEpisodes(SpotifyPodcastId request, IndexingContext indexingContext);
}