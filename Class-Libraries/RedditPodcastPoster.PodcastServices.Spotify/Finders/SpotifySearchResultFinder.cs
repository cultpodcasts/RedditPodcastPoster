using System.Net;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Finders;

public class SpotifySearchResultFinder(IEpisodePlatformMatcher platformMatcher) : ISpotifySearchResultFinder
{
    public IEnumerable<SimpleShow> FindMatchingPodcasts(string podcastName, List<SimpleShow>? podcasts)
    {
        if (podcasts == null)
        {
            return [];
        }

        return podcasts.Where(x => x.Name.ToLower().Trim() == podcastName.ToLower());
    }

    public SimpleEpisode? FindMatchingEpisodeByLength(
        string episodeTitle,
        TimeSpan episodeLength,
        IEnumerable<SimpleEpisode> episodes,
        Func<SimpleEpisode, bool>? reducer = null,
        Service? releaseAuthority = null,
        DateTime? released = null,
        bool enrichingYouTubeDiscoveredEpisode = false)
    {
        var probe = CreateProbeEpisode(episodeTitle, episodeLength, released);
        var candidates = episodes.Select(ToCatalogueEpisode).ToList();
        Func<Episode, bool>? episodeReducer = reducer == null
            ? null
            : e =>
            {
                var source = episodes.FirstOrDefault(x => x.Id == e.SpotifyId);
                return source != null && reducer(source);
            };

        // Match AppleEpisodeResolver: YouTube-discovered enrichment must not accept a sole
        // duration match without title confidence (prevents wrong-week Spotify sniping).
        var match = platformMatcher.FindCatalogueMatchByLength(
            probe,
            candidates,
            CreateLookupPodcast(releaseAuthority),
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(
                ReleaseAuthority: releaseAuthority,
                AcceptUniqueDurationWithoutTitleMatch: false,
                EnrichingYouTubeDiscoveredEpisode: enrichingYouTubeDiscoveredEpisode),
            episodeReducer);

        return match == null ? null : FindSourceEpisode(episodes, match);
    }

    public SimpleEpisode? FindMatchingEpisodeByDate(
        string episodeTitle,
        DateTime? episodeRelease,
        IEnumerable<SimpleEpisode> episodes)
    {
        var probe = CreateProbeEpisode(episodeTitle, TimeSpan.Zero, episodeRelease);
        var candidates = episodes.Select(ToCatalogueEpisode).ToList();

        var match = platformMatcher.FindCatalogueMatchByDate(
            probe,
            candidates,
            CreateLookupPodcast(releaseAuthority: null),
            episodeMatchRegex: null);

        return match == null ? null : FindSourceEpisode(episodes, match);
    }

    private static Episode CreateProbeEpisode(string title, TimeSpan length, DateTime? released) =>
        new()
        {
            Title = WebUtility.HtmlDecode(title.Trim()),
            Length = length,
            Release = released ?? DateTime.MinValue
        };

    private static Episode ToCatalogueEpisode(SimpleEpisode episode) =>
        new()
        {
            Title = WebUtility.HtmlDecode(episode.Name.Trim()),
            Length = episode.GetDuration(),
            Release = episode.GetReleaseDate(),
            SpotifyId = episode.Id
        };

    private static SimpleEpisode? FindSourceEpisode(IEnumerable<SimpleEpisode> episodes, Episode match) =>
        episodes.FirstOrDefault(x => x.Id == match.SpotifyId);

    private static Podcast CreateLookupPodcast(Service? releaseAuthority) =>
        new()
        {
            ReleaseAuthority = releaseAuthority ?? Service.Spotify
        };
}
