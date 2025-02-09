using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Finders;

public interface ISearchResultFinder
{
    SimpleEpisode? FindMatchingEpisodeByDate(
        string episodeTitle,
        DateTime? episodeRelease,
        IEnumerable<SimpleEpisode> episodes);

    IEnumerable<SimpleShow> FindMatchingPodcasts(
        string podcastName,
        List<SimpleShow>? podcasts);

    SimpleEpisode? FindMatchingEpisodeByLength(
        string episodeTitle,
        TimeSpan episodeLength,
        IEnumerable<SimpleEpisode> episodeLists,
        Func<SimpleEpisode, bool>? reducer = null);
}