using RedditPodcastPoster.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifySearcher
{
    SimpleEpisode? FindMatchingEpisode(Episode episode, IEnumerable<IEnumerable<SimpleEpisode>> episodeLists);
    IEnumerable<SimpleShow> FindMatchingPodcasts(Podcast podcast, List<SimpleShow>? podcasts);
}