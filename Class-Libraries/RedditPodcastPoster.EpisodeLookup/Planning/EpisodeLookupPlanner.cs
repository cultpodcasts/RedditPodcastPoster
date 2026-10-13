using Microsoft.Extensions.Options;

using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>
/// CENTRAL CLEARING-HOUSE: every Spotify and Apple single-episode lookup in every use case (indexer, URL submission,
/// discovery, enrich CLI, image updater) is planned here. Any change or bug fix must consider every use case and
/// starts with a business-rules test (see <c>RedditPodcastPoster.EpisodeLookup/README.md</c>).
/// <list type="number">
/// <item>No show known for the platform: Index plans nothing (a search cannot succeed and indexing never learns a
/// show id from it); every other flow searches by title only.</item>
/// <item>Small show (known count &lt;= <see cref="EpisodeLookupOptions.FullShowReadMaxEpisodes"/>): full read, any flow;
/// outside Index it is budgeted by <see cref="EpisodeLookupOptions.FullShowReadBudget"/> and followed by a title search
/// (skipped when the full read completed).</item>
/// <item>Otherwise the chain declared in <see cref="EpisodeLookupChains.LargeShow"/> for (flow, age).</item>
/// </list>
/// Unknown size counts as large. Every page read is bounded by a date, a page cap or both.
/// </summary>
public sealed class EpisodeLookupPlanner(IOptions<EpisodeLookupOptions> options, TimeProvider timeProvider)
    : IEpisodeLookupPlanner
{
    private readonly EpisodeLookupOptions _options = options.Value;

    public IReadOnlyList<CatalogueLookupStep> Plan(
        CatalogueLookup lookup,
        bool hasShow,
        int? showEpisodeCount,
        string platform)
    {
        if (!hasShow)
        {
            return lookup.Flow == EpisodeLookupFlow.Index
                ? []
                : [new CatalogueLookupStep(CatalogueLookupStrategy.TitleSearch)];
        }

        if (showEpisodeCount is { } count && count <= _options.FullShowReadMaxEpisodes)
        {
            // Index reads small shows in full, unbudgeted (it runs in the background). Every other flow serves a
            // request, so the read is time-budgeted and falls back to one title search.
            return lookup.Flow == EpisodeLookupFlow.Index
                ? [new CatalogueLookupStep(CatalogueLookupStrategy.FullShowRead, PageCap: _options.FullShowReadMaxPages)]
                :
                [
                    new CatalogueLookupStep(CatalogueLookupStrategy.FullShowRead, PageCap: _options.FullShowReadMaxPages,
                        Budget: _options.FullShowReadBudget),
                    new CatalogueLookupStep(CatalogueLookupStrategy.TitleSearch)
                ];
        }

        var age = IsRecent(lookup.Released) ? EpisodeLookupAge.Recent : EpisodeLookupAge.OldOrUndated;
        var chain = EpisodeLookupChains.LargeShow.TryGetValue((lookup.Flow, age), out var declared)
            ? declared
            : EpisodeLookupChains.LargeShow[(EpisodeLookupFlow.Submit, age)];
        DateTime? bandStart = lookup.Released is { } released
            ? EpisodeReleaseTolerance.GetSubmitMatchBand(released).Start
            : null;
        return chain.Select(strategy => ToStep(strategy, lookup, bandStart, platform)).ToList();
    }

    private CatalogueLookupStep ToStep(
        CatalogueLookupStrategy strategy, CatalogueLookup lookup, DateTime? bandStart, string platform) =>
        strategy switch
        {
            // Index: until past the earlier of the window start and the release band; 1 page when neither is known.
            CatalogueLookupStrategy.NewestPages when lookup.Flow == EpisodeLookupFlow.Index =>
                Earliest(lookup.WindowStart, bandStart) is { } stopAt
                    ? new CatalogueLookupStep(strategy, stopAt, _options.IndexMaxPages)
                    : new CatalogueLookupStep(strategy, PageCap: 1),
            // Other flows: the newest page only, for a recent release.
            CatalogueLookupStrategy.NewestPages => new CatalogueLookupStep(strategy, bandStart, 1),
            CatalogueLookupStrategy.BoundedWalk =>
                new CatalogueLookupStep(strategy, bandStart, _options.BoundedWalkPagesFor(platform)),
            _ => new CatalogueLookupStep(strategy)
        };

    private bool IsRecent(DateTime? released) =>
        released is { } value && timeProvider.GetUtcNow().UtcDateTime - value < _options.RecentWindow;

    private static DateTime? Earliest(DateTime? a, DateTime? b) =>
        a is null ? b : b is null ? a : a < b ? a : b;
}
