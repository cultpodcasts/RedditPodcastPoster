using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyItemResolver
{
    Task<FullEpisode?> FindEpisode(FindSpotifyEpisodeRequest request, IndexOptions indexOptions);
    Task<SpotifyPodcastWrapper?> FindPodcast(FindSpotifyPodcastRequest request, IndexOptions indexOptions);
    Task<IEnumerable<SimpleEpisode>> GetEpisodes(SpotifyPodcastId request, IndexOptions indexOptions);
}