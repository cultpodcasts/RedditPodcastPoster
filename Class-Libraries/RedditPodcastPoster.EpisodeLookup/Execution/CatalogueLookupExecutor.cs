using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Episodes;

using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>
/// One catalogue lookup for every audio platform:
/// <list type="number">
/// <item>Show size: the podcast's stored count for this platform, else the source's cheap probe (recorded in
/// <see cref="ICatalogueShowEpisodeCounts"/>).</item>
/// <item>Plan: <see cref="IEpisodeLookupPlanner"/> picks the strategy chain.</item>
/// <item>Index miss memory: when enabled, skip while the page-0 fingerprint matches a remembered miss within
/// <see cref="LookupEpisodeMissOptions.MissRetryAfter"/> (#1558).</item>
/// <item>Run: each step's new candidates (ids already tried are skipped; pages already read are reused) are
/// subject-classified and matched with <see cref="ICatalogueEpisodeMatcher"/>; the first match wins.</item>
/// </list>
/// Logs one line per lookup with the stage that matched (or <see cref="StageMiss"/>, or
/// <see cref="StageSkippedNoShow"/> / <see cref="StageSkippedUnchanged"/> with plan <see cref="PlanNone"/> when
/// the planner plans nothing / miss memory skips).
/// </summary>
public sealed class CatalogueLookupExecutor(
    IEpisodeLookupPlanner planner,
    ICatalogueEpisodeMatcher matcher,
    ICatalogueSubjectClassifier subjectClassifier,
    ICatalogueShowEpisodeCounts showEpisodeCounts,
    ICatalogueLookupMisses lookupMisses,
    IOptions<EpisodeLookupOptions> options,
    TimeProvider timeProvider,
    ILogger<CatalogueLookupExecutor> logger) : ICatalogueLookupExecutor
{
    public const string StageMiss = "miss";
    /// <summary>
    /// Logged when the planner returns an empty plan. Today only the Index no-show-id rule does that; a future rule that
    /// plans nothing for another reason needs its own stage value.
    /// </summary>
    public const string StageSkippedNoShow = "skipped-no-show";
    /// <summary>
    /// Index miss memory: catalogue fingerprint unchanged since the last full miss and still within the retry window.
    /// </summary>
    public const string StageSkippedUnchanged = "skipped-unchanged";
    public const string PlanNone = "none";
    public const string LogMessagePrefix = "Catalogue episode lookup:";
    public const string BudgetLogMessagePrefix = "Catalogue episode lookup budget spent:";

    private readonly LookupEpisodeMissOptions _missMemory = options.Value.MissMemory;

    public async Task<CatalogueLookupResult<TItem>> FindMatch<TItem>(
        CatalogueLookup lookup,
        ICatalogueCandidateSource<TItem> source,
        Func<TItem, bool>? reducer = null,
        CancellationToken cancellationToken = default)
        where TItem : class
    {
        var hasShow = !string.IsNullOrWhiteSpace(source.ShowId);
        var count = lookup.ShowEpisodeCount;
        if (hasShow && count is null)
        {
            count = await source.ProbeShowEpisodeCount(cancellationToken);
            Record(source, count);
        }

        var steps = planner.Plan(lookup, hasShow, count, source.Platform);
        if (steps.Count == 0)
        {
            Log(lookup, source, steps, StageSkippedNoShow, count, 0, new ClassifyTally());
            return new CatalogueLookupResult<TItem>(null, StageSkippedNoShow);
        }

        var run = new LookupRun<TItem>(source);
        var tally = new ClassifyTally();

        if (_missMemory.MissMemoryEnabled &&
            lookup.Flow == EpisodeLookupFlow.Index &&
            lookup.EpisodeId is { } episodeId &&
            lookup.PodcastId is { } podcastId &&
            hasShow)
        {
            var page0Items = await run.ReadPages(
                new CatalogueLookupStep(CatalogueLookupStrategy.NewestPages, PageCap: 1),
                cancellationToken);
            // Page 0 unreadable (guard / null): never skip — still run the plan, then clear/record.
            string? newestId = null;
            var showTotal = run.ReportedTotal ?? count;
            if (run.PagesRead > 0)
            {
                newestId = page0Items.Count > 0 ? source.GetId(page0Items[0]) : null;
                showTotal = run.ReportedTotal ?? count;
                var known = lookup.KnownMisses?.FirstOrDefault(m =>
                    m.EpisodeId == episodeId &&
                    string.Equals(m.Platform, source.Platform, StringComparison.Ordinal));
                var now = timeProvider.GetUtcNow().UtcDateTime;
                if (known != null &&
                    newestId != null &&
                    known.NewestCatalogueId == newestId &&
                    known.ShowTotal == showTotal &&
                    now - known.LastFullAttemptUtc < _missMemory.MissRetryAfter)
                {
                    Log(lookup, source, steps, StageSkippedUnchanged, count, run.PagesRead, tally);
                    return new CatalogueLookupResult<TItem>(null, StageSkippedUnchanged);
                }
            }

            var fullResult = await RunPlan(lookup, source, run, steps, tally, count, reducer, cancellationToken);
            ApplyMissMemoryOutcome(
                podcastId, episodeId, source.Platform, newestId, showTotal, fullResult);
            return fullResult;
        }

        return await RunPlan(lookup, source, run, steps, tally, count, reducer, cancellationToken);
    }

    /// <summary>
    /// After an Index plan finishes under miss memory: clear on match, record on miss (newest id may be null when
    /// page 0 was unreadable — the skip gate already requires a non-null newest id).
    /// </summary>
    private void ApplyMissMemoryOutcome<TItem>(
        Guid podcastId,
        Guid episodeId,
        string platform,
        string? newestCatalogueId,
        int? showTotal,
        CatalogueLookupResult<TItem> result)
        where TItem : class
    {
        if (result.Match != null)
        {
            lookupMisses.Clear(podcastId, episodeId, platform);
        }
        else if (result.Stage == StageMiss)
        {
            lookupMisses.RecordMiss(
                podcastId,
                episodeId,
                platform,
                newestCatalogueId,
                showTotal,
                timeProvider.GetUtcNow().UtcDateTime);
        }
    }

    private async Task<CatalogueLookupResult<TItem>> RunPlan<TItem>(
        CatalogueLookup lookup,
        ICatalogueCandidateSource<TItem> source,
        LookupRun<TItem> run,
        IReadOnlyList<CatalogueLookupStep> steps,
        ClassifyTally tally,
        int? count,
        Func<TItem, bool>? reducer,
        CancellationToken cancellationToken)
        where TItem : class
    {
        var fullReadCompleted = false;
        foreach (var step in steps)
        {
            if (step.Strategy == CatalogueLookupStrategy.TitleSearch && fullReadCompleted)
            {
                // Every episode of the show was already tried; a search cannot find anything new.
                continue;
            }

            TItem? match;
            if (step.Budget is { } budget)
            {
                (match, var spent) = await BudgetedFullRead(lookup, source, run, step, budget, reducer, tally, cancellationToken);
                if (spent)
                {
                    LogBudget(lookup, source, step, budget, run.PagesRead);
                }
            }
            else
            {
                var items = step.Strategy == CatalogueLookupStrategy.TitleSearch
                    ? await source.SearchByTitle(cancellationToken) ?? []
                    : await run.ReadPages(step, cancellationToken);
                match = await MatchStage(lookup, source, run.Untried(items), reducer, tally, cancellationToken);
            }

            if (step.Strategy == CatalogueLookupStrategy.FullShowRead && run.ReadToEnd)
            {
                // A completed full read is an exact count (Apple refreshes only this way or from its quick lookup).
                Record(source, run.TotalItemsRead);
                fullReadCompleted = true;
            }

            Record(source, run.ReportedTotal);
            if (match != null)
            {
                Log(lookup, source, steps, step.Stage, count, run.PagesRead, tally);
                return new CatalogueLookupResult<TItem>(
                    match, step.Stage, step.Strategy == CatalogueLookupStrategy.TitleSearch);
            }
        }

        Log(lookup, source, steps, StageMiss, count, run.PagesRead, tally);
        return new CatalogueLookupResult<TItem>(null, StageMiss);
    }

    private void Record<TItem>(ICatalogueCandidateSource<TItem> source, int? count)
    {
        if (count is { } value && source.ShowId is { } showId)
        {
            showEpisodeCounts.Record(source.Platform, showId, value);
        }
    }

    private async Task<TItem?> MatchStage<TItem>(
        CatalogueLookup lookup,
        ICatalogueCandidateSource<TItem> source,
        IReadOnlyList<TItem> items,
        Func<TItem, bool>? reducer,
        ClassifyTally tally,
        CancellationToken cancellationToken)
        where TItem : class
    {
        if (items.Count == 0)
        {
            return null;
        }

        var byEpisode = Map(source, items);
        var probe = CreateProbe(lookup);
        var ignoredSubjects = await Classify(probe, byEpisode.Keys.ToList(), lookup, tally, cancellationToken);
        return Match(lookup, probe, byEpisode, ignoredSubjects, reducer);
    }

    /// <summary>
    /// Full read under a time budget: page by page, each page's new candidates are classified as they arrive (that is
    /// where the time goes for YouTube-discovered episodes). If the budget is spent before the read completes the step
    /// matches only the candidates already classified and the plan moves on (title search); otherwise all candidates are
    /// matched together as usual. The budget is checked between pages, so it can be exceeded by at most one page read
    /// plus that page's classification; it is a soft cap.
    /// </summary>
    private async Task<(TItem? Match, bool BudgetSpent)> BudgetedFullRead<TItem>(
        CatalogueLookup lookup,
        ICatalogueCandidateSource<TItem> source,
        LookupRun<TItem> run,
        CatalogueLookupStep step,
        TimeSpan budget,
        Func<TItem, bool>? reducer,
        ClassifyTally tally,
        CancellationToken cancellationToken)
        where TItem : class
    {
        var deadline = timeProvider.GetUtcNow() + budget;
        var probe = CreateProbe(lookup);
        var byEpisode = new Dictionary<Episode, TItem>(ReferenceEqualityComparer.Instance);
        IReadOnlyList<string>? ignoredSubjects = lookup.IgnoredSubjects;
        var cap = step.PageCap ?? 1;
        for (var pages = 1; pages <= cap; pages++)
        {
            var before = run.PagesRead;
            var items = await run.ReadPages(step with { PageCap = pages }, cancellationToken);
            var fresh = Map(source, run.Untried(items));
            if (fresh.Count > 0)
            {
                ignoredSubjects = await Classify(probe, fresh.Keys.ToList(), lookup, tally, cancellationToken);
                foreach (var (episode, item) in fresh)
                {
                    byEpisode[episode] = item;
                }
            }

            if (run.ReadToEnd || run.PagesRead == before)
            {
                break;
            }

            if (timeProvider.GetUtcNow() >= deadline)
            {
                // Spent: match what was already classified (no extra cost), then let the plan move on.
                return (MatchAll(lookup, probe, byEpisode, ignoredSubjects, reducer), true);
            }
        }

        return (MatchAll(lookup, probe, byEpisode, ignoredSubjects, reducer), false);
    }

    /// <summary>
    /// Runs the classifier and tallies what the lookup line reports: candidates handed over, candidates handed to
    /// subject/guest classification (only when enriching a YouTube-discovered episode; otherwise the classifier returns
    /// the ignore list and does no subject/guest work; a candidate served from the classifier's per-scope memo still
    /// counts) and the time spent inside the call, measured with the injected <see cref="TimeProvider"/>.
    /// The tally is only logged when the lookup completes: if the classifier throws, the exception propagates and no
    /// lookup line (and so no tally) is written.
    /// </summary>
    private async Task<IReadOnlyList<string>?> Classify(
        Episode probe,
        IReadOnlyList<Episode> candidates,
        CatalogueLookup lookup,
        ClassifyTally tally,
        CancellationToken cancellationToken)
    {
        tally.Candidates += candidates.Count;
        // This condition must match the one in CatalogueSubjectClassifier.ClassifyAsync, so step 2 does not drift from it.
        if (lookup.EnrichingYouTubeDiscoveredEpisode)
        {
            tally.Classified += candidates.Count;
        }

        var started = timeProvider.GetTimestamp();
        var ignoredSubjects = await subjectClassifier.ClassifyAsync(probe, candidates, lookup, cancellationToken);
        tally.ClassifyMs += (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;
        return ignoredSubjects;
    }

    private TItem? MatchAll<TItem>(CatalogueLookup lookup, Episode probe, Dictionary<Episode, TItem> byEpisode,
        IReadOnlyList<string>? ignoredSubjects, Func<TItem, bool>? reducer)
        where TItem : class =>
        byEpisode.Count == 0 ? null : Match(lookup, probe, byEpisode, ignoredSubjects, reducer);

    private static Dictionary<Episode, TItem> Map<TItem>(ICatalogueCandidateSource<TItem> source, IEnumerable<TItem> items)
    {
        var byEpisode = new Dictionary<Episode, TItem>(ReferenceEqualityComparer.Instance);
        foreach (var item in items)
        {
            byEpisode[source.ToCatalogueEpisode(item)] = item;
        }

        return byEpisode;
    }

    private TItem? Match<TItem>(
        CatalogueLookup lookup,
        Episode probe,
        Dictionary<Episode, TItem> byEpisode,
        IReadOnlyList<string>? ignoredSubjects,
        Func<TItem, bool>? reducer)
        where TItem : class
    {
        Func<Episode, bool>? episodeReducer = reducer == null ? null : e => reducer(byEpisode[e]);
        var match = matcher.Match(lookup, probe, byEpisode.Keys.ToList(), ignoredSubjects, episodeReducer);
        return match == null ? null : byEpisode[match];
    }

    private void LogBudget<TItem>(
        CatalogueLookup lookup,
        ICatalogueCandidateSource<TItem> source,
        CatalogueLookupStep step,
        TimeSpan budget,
        int pagesRead) =>
        logger.LogInformation(
            BudgetLogMessagePrefix +
            " platform='{Platform}' flow='{Flow}' podcast='{PodcastName}' episode='{EpisodeTitle}' stage='{Stage}' budget-seconds={BudgetSeconds} pages-read={PagesRead}; falling back to the next step.",
            source.Platform, lookup.Flow, lookup.PodcastName, lookup.Title, step.Stage, (int)budget.TotalSeconds, pagesRead);

    private static Episode CreateProbe(CatalogueLookup lookup) =>
        new()
        {
            Title = WebUtility.HtmlDecode(lookup.Title.Trim()),
            Description = lookup.Description?.Trim() ?? string.Empty,
            Length = lookup.Length ?? TimeSpan.Zero,
            ReleaseUtc = lookup.Released ?? DateTime.MinValue
        };

    private void Log<TItem>(
        CatalogueLookup lookup,
        ICatalogueCandidateSource<TItem> source,
        IReadOnlyList<CatalogueLookupStep> steps,
        string stage,
        int? showEpisodeCount,
        int pagesRead,
        ClassifyTally tally) =>
        logger.LogInformation(
            LogMessagePrefix +
            " platform='{Platform}' flow='{Flow}' podcast='{PodcastName}' episode='{EpisodeTitle}' plan='{Plan}' stage='{Stage}' show-episodes={ShowEpisodeCount} pages-read={PagesRead} candidates={Candidates} classified={Classified} classify-ms={ClassifyMs}.",
            source.Platform, lookup.Flow, lookup.PodcastName, lookup.Title,
            steps.Count == 0 ? PlanNone : string.Join(">", steps.Select(x => x.Stage)), stage, showEpisodeCount, pagesRead, tally.Candidates, tally.Classified, tally.ClassifyMs);

    /// <summary>Per-lookup classification totals for the lookup line (sum over steps).</summary>
    private sealed class ClassifyTally
    {
        public int Candidates;
        public int Classified;
        public long ClassifyMs;
    }

    /// <summary>Per-lookup page reader: pages are read newest-first once and reused across steps.</summary>
    private sealed class LookupRun<TItem>(ICatalogueCandidateSource<TItem> source)
    {
        private readonly List<CatalogueShowPage<TItem>> _pages = [];
        private readonly HashSet<string> _tried = new(StringComparer.Ordinal);
        private bool _exhausted;

        public bool ReadToEnd { get; private set; }
        public int? ReportedTotal { get; private set; }
        public int PagesRead => _pages.Count;
        public int TotalItemsRead => _pages.Sum(x => x.Items.Count);

        public async Task<IReadOnlyList<TItem>> ReadPages(CatalogueLookupStep step, CancellationToken cancellationToken)
        {
            var cap = step.PageCap ?? 1;
            var index = 0;
            while (index < cap)
            {
                if (index >= _pages.Count)
                {
                    if (_exhausted)
                    {
                        break;
                    }

                    var page = await source.GetNewestPage(index, cancellationToken);
                    if (page == null)
                    {
                        _exhausted = true;
                        break;
                    }

                    _pages.Add(page);
                    ReportedTotal = page.TotalEpisodes ?? ReportedTotal;
                    if (!page.HasMore)
                    {
                        _exhausted = true;
                        ReadToEnd = true;
                    }
                }

                var current = _pages[index++];
                if (step.Strategy != CatalogueLookupStrategy.FullShowRead &&
                    step.StopAt is { } stopAt && current.IsNewestFirst && current.Items.Count > 0 &&
                    current.Items.Min(x => source.ToCatalogueEpisode(x).ReleaseUtc) < stopAt)
                {
                    break;
                }
            }

            return _pages.Take(index).SelectMany(x => x.Items).ToList();
        }

        public IReadOnlyList<TItem> Untried(IEnumerable<TItem> items) =>
            items.Where(x => x != null && _tried.Add(source.GetId(x))).ToList();
    }
}
