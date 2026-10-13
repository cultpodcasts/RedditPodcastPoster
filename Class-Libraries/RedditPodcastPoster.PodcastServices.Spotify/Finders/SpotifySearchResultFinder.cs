using System.Net;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.Text.Sanitisers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Finders;

/// <summary>
/// Spotify podcast-name matching and release-date episode matching for podcast resolution.
/// Episode lookups for enrichment go through the shared <c>ICatalogueCandidatePolicy</c> instead.
/// </summary>
public class SpotifySearchResultFinder(
    IEpisodePlatformMatcher platformMatcher,
    IHtmlSanitiser htmlSanitiser) : ISpotifySearchResultFinder
{
    public IEnumerable<SimpleShow> FindMatchingPodcasts(string podcastName, List<SimpleShow>? podcasts)
    {
        if (podcasts == null)
        {
            return [];
        }

        return podcasts.Where(x => x.Name.ToLower().Trim() == podcastName.ToLower());
    }

    public SimpleEpisode? FindMatchingEpisodeByDate(
        string episodeTitle,
        DateTime? episodeRelease,
        IEnumerable<SimpleEpisode> episodes)
    {
        var probe = CreateProbeEpisode(episodeTitle, TimeSpan.Zero, episodeRelease, description: null);
        var candidates = episodes.Select(e => ToCatalogueEpisode(e, htmlSanitiser)).ToList();

        var match = platformMatcher.FindCatalogueMatchByDate(
            probe,
            candidates,
            CreateLookupPodcast(),
            episodeMatchRegex: null);

        return match == null ? null : FindSourceEpisode(episodes, match);
    }

    private static Episode CreateProbeEpisode(
        string title,
        TimeSpan length,
        DateTime? released,
        string? description) =>
        new()
        {
            Title = WebUtility.HtmlDecode(title.Trim()),
            Description = description?.Trim() ?? string.Empty,
            Length = length,
            ReleaseUtc = released ?? DateTime.MinValue
        };

    private static Episode ToCatalogueEpisode(SimpleEpisode episode, IHtmlSanitiser htmlSanitiser)
    {
        var mapped = new Episode
        {
            Title = WebUtility.HtmlDecode(episode.Name.Trim()),
            Description = htmlSanitiser.Sanitise(episode.HtmlDescription ?? string.Empty),
            Length = episode.GetDuration(),
            ReleaseUtc = episode.GetReleaseDate()
        };
        EpisodeServicePresence.SetSpotifyIdentity(mapped, episode.Id);
        return mapped;
    }

    private static SimpleEpisode? FindSourceEpisode(IEnumerable<SimpleEpisode> episodes, Episode match) =>
        episodes.FirstOrDefault(x => x.Id == EpisodeServicePresence.SpotifyEpisodeId(match));

    private static Podcast CreateLookupPodcast() =>
        new()
        {
            ReleaseAuthority = Service.Spotify
        };
}
