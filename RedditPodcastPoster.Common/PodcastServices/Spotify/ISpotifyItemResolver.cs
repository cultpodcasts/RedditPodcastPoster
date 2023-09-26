using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyItemResolver
{
    Task<SpotifyEpisodeWrapper> FindEpisode(FindSpotifyEpisodeRequest request);
    Task<SpotifyPodcastWrapper> FindPodcast(FindSpotifyPodcastRequest request);
    Task<IEnumerable<SimpleEpisode>> GetEpisodes(string spotifyId, DateTime? releasedSince);
}