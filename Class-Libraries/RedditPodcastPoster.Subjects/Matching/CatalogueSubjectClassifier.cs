using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Subjects.Matching;

/// <summary>
/// Shared subject classification for catalogue lookups (formerly duplicated in the Spotify finder and Apple
/// resolver). Ignored subjects are the podcast list unioned with the language list; subjects are classified on the
/// probe and every candidate only when enriching a YouTube-discovered episode, where the multi-criteria scorer uses them.
/// Candidate subjects are memoised for the scope (one apply, submit or index pass): the same show page is often scored
/// for several lookups (submission 4a7d8838 classified The Compendium's 185 + 159 episodes twice, ~45 s each time).
/// </summary>
public sealed class CatalogueSubjectClassifier(
    ISubjectMatcher subjectMatcher,
    IAsyncInstance<ITitleCasingRulesProvider> titleCasingRulesProvider) : ICatalogueSubjectClassifier
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Options, string Title, string Description, DateTime Release), List<string>>
        _candidateSubjects = new();

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

        var options = new SubjectEnrichmentOptions(
            IgnoredAssociatedSubjects: null,
            IgnoredSubjects: ignored?.ToArray(),
            DefaultSubject: lookup.DefaultSubject,
            DescriptionRegex: string.Empty);
        probe.Subjects = await MatchSubjectNamesAsync(probe, options, cancellationToken);
        var optionsKey = string.Join("|", lookup.DefaultSubject ?? string.Empty,
            string.Join(",", (ignored ?? []).Order(StringComparer.OrdinalIgnoreCase)));
        foreach (var candidate in candidates)
        {
            var key = (optionsKey, candidate.Title ?? string.Empty, candidate.Description ?? string.Empty, candidate.ReleaseUtc);
            if (!_candidateSubjects.TryGetValue(key, out var subjects))
            {
                subjects = await MatchSubjectNamesAsync(candidate, options, cancellationToken);
                _candidateSubjects[key] = subjects;
            }

            candidate.Subjects = [.. subjects];
        }

        return ignored;
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
}
