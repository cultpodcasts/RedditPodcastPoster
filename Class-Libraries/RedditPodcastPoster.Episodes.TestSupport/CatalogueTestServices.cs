using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.TestSupport;

/// <summary>Real shared catalogue policy and matcher for platform adapter tests.</summary>
public static class CatalogueTestServices
{
    public static CatalogueCandidatePolicy CreatePolicy(
        ICatalogueSubjectClassifier? subjectClassifier = null,
        TimeProvider? timeProvider = null,
        CatalogueCandidatePolicyOptions? options = null,
        IEpisodePlatformMatcher? platformMatcher = null) =>
        new(
            new CatalogueEpisodeMatcher(platformMatcher ?? EpisodeDomainTestServices.CreatePlatformMatcher()),
            subjectClassifier ?? new NoCatalogueSubjectClassifier(),
            Options.Create(options ?? new CatalogueCandidatePolicyOptions()),
            timeProvider ?? TimeProvider.System,
            NullLogger<CatalogueCandidatePolicy>.Instance);
}

/// <summary>Deterministic clock for recent-window rules.</summary>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), TimeSpan.Zero);
}

/// <summary>
/// Subject classifier stub: when enriching a YouTube-discovered episode, any episode whose title contains a key
/// gets that key's subjects. Mirrors the real classifier's contract without the subject repository.
/// </summary>
public sealed class TitleSubstringSubjectClassifier(IReadOnlyDictionary<string, IReadOnlyList<string>> map)
    : ICatalogueSubjectClassifier
{
    public Task<IReadOnlyList<string>?> ClassifyAsync(
        Episode probe,
        IReadOnlyList<Episode> candidates,
        CatalogueLookup lookup,
        CancellationToken cancellationToken)
    {
        if (lookup.EnrichingYouTubeDiscoveredEpisode)
        {
            foreach (var episode in candidates.Prepend(probe))
            {
                episode.Subjects = map
                    .Where(kv => episode.Title.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(kv => kv.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        return Task.FromResult(lookup.IgnoredSubjects);
    }
}
