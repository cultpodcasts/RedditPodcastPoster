using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyItemResolver
{
    Task<FullEpisode?> FindEpisode(FindSpotifyEpisodeRequest request);
    Task<SpotifyPodcastWrapper> FindPodcast(FindSpotifyPodcastRequest request);
    Task<IEnumerable<SimpleEpisode>> GetEpisodes(GetSpotifyPodcastEpisodesRequest request);
}