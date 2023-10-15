using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyEpisodeResolver
{
    Task<FullEpisode?> FindEpisode(FindSpotifyEpisodeRequest request, IndexingContext indexingContext);
    Task<IEnumerable<SimpleEpisode>?> GetEpisodes(SpotifyPodcastId request, IndexingContext indexingContext);
}