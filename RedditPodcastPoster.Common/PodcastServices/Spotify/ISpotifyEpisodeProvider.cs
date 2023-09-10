using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;


public interface ISpotifyEpisodeProvider
{
    Task<IList<Episode>> GetEpisodes(SpotifyGetEpisodesRequest request);
}