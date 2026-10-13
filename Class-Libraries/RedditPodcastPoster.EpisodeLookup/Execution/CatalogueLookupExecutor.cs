using System.Net;
using Microsoft.Extensions.Logging;
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
/// <item>Run: each step's new candidates (ids already tried are skipped; pages already read are reused) are
/// subject-classified and matched with <see cref="ICatalogueEpisodeMatcher"/>; the first match wins.</item>
/// </list>
/// Logs one line per lookup with the stage that matched (or <see cref="StageMiss"/>).
/// </summary>
public sealed class CatalogueLookupExecutor(
    IEpisodeLookupPlanner planner,
    ICatalogueEpisodeMatcher matcher,
    ICatalogueSubjectClassifier subjectClassifier,
    ICatalogueShowEpisodeCounts showEpisodeCounts,
    ILogger<CatalogueLookupExecutor> logger) : ICatalogueLookupExecutor
{
    public const string StageMiss = "miss";
    public const string LogMessagePrefix = "Catalogue episode lookup:";

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
        var run = new LookupRun<TItem>(source);
        foreach (var step in steps)
        {
            var items = step.Strategy == CatalogueLookupStrategy.TitleSearch
                ? await source.SearchByTitle(cancellationToken) ?? []
                : await run.ReadPages(step, cancellationToken);
            if (step.Strategy == CatalogueLookupStrategy.FullShowRead && run.ReadToEnd)
            {
                // A completed full read is an exact count (Apple refreshes only this way or from its quick lookup).
                Record(source, run.TotalItemsRead);
            }

            Record(source, run.ReportedTotal);
            var match = await MatchStage(lookup, source, run.Untried(items), reducer, cancellationToken);
            if (match != null)
            {
                Log(lookup, source, steps, step.Stage, count, run.PagesRead);
                return new CatalogueLookupResult<TItem>(
                    match, step.Stage, step.Strategy == CatalogueLookupStrategy.TitleSearch);
            }
        }

        Log(lookup, source, steps, StageMiss, count, run.PagesRead);
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
        CancellationToken cancellationToken)
        where TItem : class
    {
        if (items.Count == 0)
        {
            return null;
        }

        var byEpisode = new Dictionary<Episode, TItem>(ReferenceEqualityComparer.Instance);
        foreach (var item in items)
        {
            byEpisode[source.ToCatalogueEpisode(item)] = item;
        }

        var candidates = byEpisode.Keys.ToList();
        var probe = CreateProbe(lookup);
        var ignoredSubjects = await subjectClassifier.ClassifyAsync(probe, candidates, lookup, cancellationToken);
        Func<Episode, bool>? episodeReducer = reducer == null ? null : e => reducer(byEpisode[e]);
        var match = matcher.Match(lookup, probe, candidates, ignoredSubjects, episodeReducer);
        return match == null ? null : byEpisode[match];
    }

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
        int pagesRead) =>
        logger.LogInformation(
            LogMessagePrefix +
            " platform='{Platform}' flow='{Flow}' podcast='{PodcastName}' episode='{EpisodeTitle}' plan='{Plan}' stage='{Stage}' show-episodes={ShowEpisodeCount} pages-read={PagesRead}.",
            source.Platform, lookup.Flow, lookup.PodcastName, lookup.Title,
            string.Join(">", steps.Select(x => x.Stage)), stage, showEpisodeCount, pagesRead);

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
