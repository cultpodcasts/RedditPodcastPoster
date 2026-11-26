using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.TestSupport;

/// <summary>Real shared planner, executor and matcher for platform adapter tests.</summary>
public static class CatalogueTestServices
{
    public static EpisodeLookupPlanner CreatePlanner(TimeProvider? timeProvider = null, EpisodeLookupOptions? options = null) =>
        new(Options.Create(options ?? new EpisodeLookupOptions()), timeProvider ?? TimeProvider.System);

    public static CatalogueLookupExecutor CreateExecutor(
        ICatalogueSubjectClassifier? subjectClassifier = null,
        TimeProvider? timeProvider = null,
        EpisodeLookupOptions? options = null,
        IEpisodePlatformMatcher? platformMatcher = null,
        ICatalogueShowEpisodeCounts? showEpisodeCounts = null,
        ILogger<CatalogueLookupExecutor>? logger = null) =>
        new(
            CreatePlanner(timeProvider, options),
            new CatalogueEpisodeMatcher(platformMatcher ?? EpisodeDomainTestServices.CreatePlatformMatcher()),
            subjectClassifier ?? new NoCatalogueSubjectClassifier(),
            showEpisodeCounts ?? new CatalogueShowEpisodeCounts(),
            timeProvider ?? TimeProvider.System,
            logger ?? NullLogger<CatalogueLookupExecutor>.Instance);

    /// <summary>Guard with the configured (default 10 s) timeout and a null logger.</summary>
    public static CatalogueRequestGuard CreateGuard(TimeProvider? timeProvider = null, EpisodeLookupOptions? options = null) =>
        new(timeProvider ?? TimeProvider.System, Options.Create(options ?? new EpisodeLookupOptions()),
            NullLogger<CatalogueRequestGuard>.Instance);
}

/// <summary>Deterministic clock for recent-window rules.</summary>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), TimeSpan.Zero);
}

/// <summary>
/// Clock the fakes advance, so budgets and measured durations are deterministic. <see cref="Advance"/> moves both the
/// wall clock (<see cref="GetUtcNow"/>) and the timestamp (<see cref="GetTimestamp"/>, 1 tick = 100 ns) together.
/// </summary>
public sealed class ManualTimeProvider(DateTime start) : TimeProvider
{
    private DateTimeOffset _now = new(DateTime.SpecifyKind(start, DateTimeKind.Utc), TimeSpan.Zero);
    private long _timestamp;

    public void Advance(TimeSpan by)
    {
        _now += by;
        _timestamp += by.Ticks;
    }

    public override DateTimeOffset GetUtcNow() => _now;
    public override long GetTimestamp() => _timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
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
