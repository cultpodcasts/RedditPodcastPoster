using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifySearcher
{
    SimpleEpisode? FindMatchingEpisodeByDate(
        string episodeTitle,
        DateTime? episodeRelease,
        IEnumerable<IEnumerable<SimpleEpisode>> episodeLists);

    IEnumerable<SimpleShow> FindMatchingPodcasts(
        string podcastName,
        List<SimpleShow>? podcasts);

    SimpleEpisode? FindMatchingEpisodeByLength(
        string episodeTitle,
        TimeSpan episodeLength,
        IList<IList<SimpleEpisode>> episodeLists);
}