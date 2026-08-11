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

    Task<SimpleEpisode?> FindMatchingEpisodeByLength(
        string episodeTitle,
        TimeSpan episodeLength,
        IEnumerable<SimpleEpisode> episodeLists,
        Func<SimpleEpisode, bool>? reducer = null,
        Service? releaseAuthority = null,
        DateTime? released = null,
        bool enrichingYouTubeDiscoveredEpisode = false,
        string? episodeDescription = null,
        string? defaultSubject = null,
        IReadOnlyList<string>? ignoredSubjects = null,
        string? language = null,
        CancellationToken cancellationToken = default);
}
