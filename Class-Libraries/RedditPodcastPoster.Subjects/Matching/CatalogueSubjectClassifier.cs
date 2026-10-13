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
/// </summary>
public sealed class CatalogueSubjectClassifier(
    ISubjectMatcher subjectMatcher,
    IAsyncInstance<ITitleCasingRulesProvider> titleCasingRulesProvider) : ICatalogueSubjectClassifier
{
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
        foreach (var candidate in candidates)
        {
            candidate.Subjects = await MatchSubjectNamesAsync(candidate, options, cancellationToken);
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
