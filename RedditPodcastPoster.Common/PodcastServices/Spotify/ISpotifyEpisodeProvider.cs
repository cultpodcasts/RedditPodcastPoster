using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyEpisodeProvider
{
    Task<IList<Episode>?> GetEpisodes(Podcast podcast, DateTime? processRequestReleasedSince);

}