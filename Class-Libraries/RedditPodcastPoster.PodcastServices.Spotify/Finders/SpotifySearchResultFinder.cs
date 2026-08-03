using System.Net;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text.Sanitisers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Finders;

public class SpotifySearchResultFinder(
    IEpisodePlatformMatcher platformMatcher,
    ISubjectMatcher subjectMatcher,
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

    public async Task<SimpleEpisode?> FindMatchingEpisodeByLength(
        string episodeTitle,
        TimeSpan episodeLength,
        IEnumerable<SimpleEpisode> episodes,
        Func<SimpleEpisode, bool>? reducer = null,
        Service? releaseAuthority = null,
        DateTime? released = null,
        bool enrichingYouTubeDiscoveredEpisode = false,
        string? episodeDescription = null,
        string? defaultSubject = null,
        IReadOnlyList<string>? ignoredSubjects = null,
        CancellationToken cancellationToken = default)
    {
        var probe = CreateProbeEpisode(episodeTitle, episodeLength, released, episodeDescription);
        var candidates = episodes.Select(e => ToCatalogueEpisode(e, htmlSanitiser)).ToList();
        Func<Episode, bool>? episodeReducer = reducer == null
            ? null
            : e =>
            {
                var source = episodes.FirstOrDefault(x => x.Id == e.SpotifyId);
                return source != null && reducer(source);
            };

        if (enrichingYouTubeDiscoveredEpisode)
        {
            await ClassifySubjectsAsync(probe, candidates, defaultSubject, ignoredSubjects, cancellationToken);
        }

        var match = platformMatcher.FindCatalogueMatchByLength(
            probe,
            candidates,
            CreateLookupPodcast(releaseAuthority),
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(
                ReleaseAuthority: releaseAuthority,
                AcceptUniqueDurationWithoutTitleMatch: false,
                EnrichingYouTubeDiscoveredEpisode: enrichingYouTubeDiscoveredEpisode,
                DefaultSubject: defaultSubject,
                IgnoredSubjects: ignoredSubjects),
            episodeReducer);

        return match == null ? null : FindSourceEpisode(episodes, match);
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
            CreateLookupPodcast(releaseAuthority: null),
            episodeMatchRegex: null);

        return match == null ? null : FindSourceEpisode(episodes, match);
    }

    private async Task ClassifySubjectsAsync(
        Episode probe,
        IList<Episode> candidates,
        string? defaultSubject,
        IReadOnlyList<string>? ignoredSubjects,
        CancellationToken cancellationToken)
    {
        var options = new SubjectEnrichmentOptions(
            IgnoredAssociatedSubjects: null,
            IgnoredSubjects: ignoredSubjects?.ToArray(),
            DefaultSubject: defaultSubject,
            DescriptionRegex: string.Empty);

        probe.Subjects = await MatchSubjectNamesAsync(probe, options, cancellationToken);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidate.Subjects = await MatchSubjectNamesAsync(candidate, options, cancellationToken);
        }
    }

    private async Task<List<string>> MatchSubjectNamesAsync(
        Episode episode,
        SubjectEnrichmentOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var matches = await subjectMatcher.MatchSubjects(episode, options);
        return matches
            .Select(x => x.Subject.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
            Release = released ?? DateTime.MinValue
        };

    private static Episode ToCatalogueEpisode(SimpleEpisode episode, IHtmlSanitiser htmlSanitiser) =>
        new()
        {
            Title = WebUtility.HtmlDecode(episode.Name.Trim()),
            Description = htmlSanitiser.Sanitise(episode.HtmlDescription ?? string.Empty),
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
