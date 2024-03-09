using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISearchResultFinder
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
        IList<IList<SimpleEpisode>> episodeLists,
        Func<SimpleEpisode, bool>? reducer = null);
}