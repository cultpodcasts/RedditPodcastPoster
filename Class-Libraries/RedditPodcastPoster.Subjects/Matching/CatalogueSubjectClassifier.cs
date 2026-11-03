using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Services;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Subjects.Matching;

/// <summary>
/// Shared subject (and match-time guest) classification for catalogue lookups. Ignored subjects are the podcast
/// list unioned with the language list. When enriching a YouTube-discovered episode, descriptions are stripped with
/// the podcast <see cref="CatalogueLookup.DescriptionRegex"/>, subjects are classified for the multi-criteria scorer,
/// and recognised People names are written onto transient <see cref="Episode.Guests"/> for scoring only (never saved
/// by the lookup path). Candidate subjects/guests are memoised for the DI scope.
/// </summary>
public sealed class CatalogueSubjectClassifier(
    ISubjectMatcher subjectMatcher,
    IAsyncInstance<ITitleCasingRulesProvider> titleCasingRulesProvider,
    ITextSanitiser textSanitiser,
    IEnumerable<IPersonService> personServices) : ICatalogueSubjectClassifier
{
    private readonly IPersonService? _personService = personServices.FirstOrDefault();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Options, string Title, string Description, DateTime Release), List<string>>
        _candidateSubjects = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Title, string Description, DateTime Release), string[]>
        _candidateGuests = new();

    public async Task<IReadOnlyList<string>?> ClassifyAsync(
        Episode probe,
        IReadOnlyList<Episode> candidates,
        CatalogueLookup lookup,
        CancellationToken cancellationToken)
    {
        var provider = await titleCasingRulesProvider.GetAsync(cancellationToken);
        var languageIgnored = await provider.GetIgnoredSubjectsAsync(lookup.Language, cancellationToken);
        var ignored = SubjectEnrichmentOptionsFactory.UnionIgnoreLists(lookup.IgnoredSubjects, languageIgnored);
        if (!lookup.EnrichingYouTubeDiscoveredEpisode)
        {
            return ignored;
        }

        // Per-episode Create: {episode-title} expands from that row's title; token-free patterns use
        // Podcast.DescriptionFlags (IgnoreCase | Singleline) so website presets with .* / \z strip like posting.
        ApplyDescriptionRegex(probe, lookup.DescriptionRegex, lookup.PodcastName);
        foreach (var candidate in candidates)
        {
            ApplyDescriptionRegex(candidate, lookup.DescriptionRegex, lookup.PodcastName);
        }

        var options = new SubjectEnrichmentOptions(
            IgnoredAssociatedSubjects: null,
            IgnoredSubjects: ignored?.ToArray(),
            DefaultSubject: lookup.DefaultSubject,
            DescriptionRegex: lookup.DescriptionRegex ?? string.Empty,
            PodcastName: lookup.PodcastName);
        probe.Subjects = await MatchSubjectNamesAsync(probe, options, cancellationToken);
        probe.Guests = await MatchGuestNamesAsync(probe, cancellationToken);

        var optionsKey = string.Join("|",
            lookup.DefaultSubject ?? string.Empty,
            lookup.DescriptionRegex ?? string.Empty,
            string.Join(",", (ignored ?? []).Order(StringComparer.OrdinalIgnoreCase)));
        foreach (var candidate in candidates)
        {
            var subjectKey = (optionsKey, candidate.Title ?? string.Empty, candidate.Description ?? string.Empty,
                candidate.ReleaseUtc);
            if (!_candidateSubjects.TryGetValue(subjectKey, out var subjects))
            {
                subjects = await MatchSubjectNamesAsync(candidate, options, cancellationToken);
                _candidateSubjects[subjectKey] = subjects;
            }

            candidate.Subjects = [.. subjects];

            var guestKey = (candidate.Title ?? string.Empty, candidate.Description ?? string.Empty,
                candidate.ReleaseUtc);
            if (!_candidateGuests.TryGetValue(guestKey, out var guests))
            {
                guests = await MatchGuestNamesAsync(candidate, cancellationToken);
                _candidateGuests[guestKey] = guests;
            }

            candidate.Guests = guests;
        }

        return ignored;
    }

    private void ApplyDescriptionRegex(Episode episode, string? descriptionRegexPattern, string? podcastName)
    {
        if (string.IsNullOrWhiteSpace(descriptionRegexPattern) ||
            string.IsNullOrWhiteSpace(episode.Description))
        {
            return;
        }

        var descriptionRegex = DescriptionRegexTokens.Create(
            descriptionRegexPattern,
            episode.Title,
            podcastName);
        if (descriptionRegex == null)
        {
            return;
        }

        episode.Description = textSanitiser.SanitiseDescription(episode.Description, descriptionRegex);
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

    private async Task<string[]> MatchGuestNamesAsync(Episode episode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_personService is null)
        {
            return [];
        }

        // Same term-length floor as GuestEnrichmentOptions.Default — match-time only, never persisted.
        const int minTermLength = 4;
        var matches = await _personService.MatchEpisode(episode, withDescription: true);
        return matches
            .Where(m => m.MatchResults.Any(r =>
                !string.IsNullOrWhiteSpace(r.Term) && r.Term.Trim().Length >= minTermLength))
            .Select(x => x.Person.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
