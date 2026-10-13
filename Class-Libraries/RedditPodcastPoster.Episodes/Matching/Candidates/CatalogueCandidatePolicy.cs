using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// One catalogue lookup for every audio platform. Stages, in order:
/// <list type="number">
/// <item><b>Recent show feed</b>: when the show is known and the release is within
/// <see cref="CatalogueCandidatePolicyOptions.RecentWindow"/> (or unknown), one page of the newest
/// <see cref="CatalogueCandidatePolicyOptions.RecentShowEpisodesPageSize"/> episodes. The feed does not depend on
/// the incoming title, so a YouTube title that differs from the audio title still finds its episode
/// (Steve Hassan / Ben McKenzie, 12 Oct 2026).</item>
/// <item><b>Search fallback</b>: otherwise, or when the feed has no match, the platform's broader lookup,
/// minus candidates already tried.</item>
/// </list>
/// Each stage classifies subjects (<see cref="ICatalogueSubjectClassifier"/>) and matches through the shared
/// <see cref="ICatalogueEpisodeMatcher"/>, so platforms differ only in how they fetch and map.
/// </summary>
public sealed class CatalogueCandidatePolicy(
    ICatalogueEpisodeMatcher matcher,
    ICatalogueSubjectClassifier subjectClassifier,
    IOptions<CatalogueCandidatePolicyOptions> options,
    TimeProvider timeProvider,
    ILogger<CatalogueCandidatePolicy> logger) : ICatalogueCandidatePolicy
{
    public const string StageRecentShowFeed = "recent-show-feed";
    public const string StageSearchFallback = "search-fallback";

    private readonly CatalogueCandidatePolicyOptions _options = options.Value;

    public async Task<TItem?> FindMatch<TItem>(
        CatalogueLookup lookup,
        ICatalogueCandidateSource<TItem> source,
        Func<TItem, bool>? reducer = null,
        CancellationToken cancellationToken = default)
        where TItem : class
    {
        var tried = new HashSet<string>(StringComparer.Ordinal);
        var feedCount = 0;
        if (source.HasShow && IsRecent(lookup.Released))
        {
            var recent = Untried(await source.GetRecentShowEpisodes(_options.RecentShowEpisodesPageSize, cancellationToken), source, tried);
            feedCount = recent.Count;
            var recentMatch = await MatchStage(lookup, source, recent, reducer, cancellationToken);
            if (recentMatch != null)
            {
                Log(lookup, source, StageRecentShowFeed, feedCount, 0);
                return recentMatch;
            }
        }

        var searched = Untried(await source.SearchEpisodes(cancellationToken), source, tried);
        var searchMatch = await MatchStage(lookup, source, searched, reducer, cancellationToken);
        Log(lookup, source, searchMatch == null ? "miss" : StageSearchFallback, feedCount, searched.Count);
        return searchMatch;
    }

    public bool IsRecent(DateTime? released) =>
        released is not { } value ||
        timeProvider.GetUtcNow().UtcDateTime - value < _options.RecentWindow;

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

    private static List<TItem> Untried<TItem>(
        IReadOnlyList<TItem>? items,
        ICatalogueCandidateSource<TItem> source,
        HashSet<string> tried) =>
        (items ?? []).Where(x => x != null && tried.Add(source.GetId(x))).ToList();

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
        string stage,
        int feedCandidates,
        int searchCandidates) =>
        logger.LogInformation(
            "Catalogue candidate lookup: platform='{Platform}' podcast='{PodcastName}' episode='{EpisodeTitle}' stage='{Stage}' feed-candidates={FeedCandidates} search-candidates={SearchCandidates}.",
            source.Platform, lookup.PodcastName, lookup.Title, stage, feedCandidates, searchCandidates);
}
