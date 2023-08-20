using RedditPodcastPoster.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyItemResolver
{
    Task<SpotifyEpisodeWrapper> FindEpisode(Podcast podcast, Episode episode);
    Task<SpotifyPodcastWrapper> FindPodcast(Podcast podcast);
    Task<IEnumerable<SimpleEpisode>> GetEpisodes(Podcast podcast);
}