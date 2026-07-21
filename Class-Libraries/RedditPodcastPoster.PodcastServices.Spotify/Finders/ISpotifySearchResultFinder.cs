using RedditPodcastPoster.Models.Podcasts;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Finders;

public interface ISpotifySearchResultFinder
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
        Func<SimpleEpisode, bool>? reducer = null,
        Service? releaseAuthority = null,
        DateTime? released = null,
        bool enrichingYouTubeDiscoveredEpisode = false);
}