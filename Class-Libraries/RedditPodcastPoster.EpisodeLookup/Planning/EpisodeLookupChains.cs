using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>Release age bucket used to pick a chain.</summary>
public enum EpisodeLookupAge
{
    /// <summary>Released within <see cref="EpisodeLookupOptions.RecentWindow"/>.</summary>
    Recent,

    /// <summary>Older, or no release date.</summary>
    OldOrUndated
}

/// <summary>
/// THE one place where each use case's strategy chain for a large or unknown-size show is declared.
/// To add a use case: add an <see cref="EpisodeLookupFlow"/> value, set it in the caller, and add its rows here.
/// To reorder or add a strategy: edit the row. Either way, add a case to <c>EpisodeLookupPlannerRules</c> first.
/// Rules that apply before this table (in <see cref="EpisodeLookupPlanner"/>): a known episode id is fetched
/// directly; no show id means title search only; a known show of at most
/// <see cref="EpisodeLookupOptions.FullShowReadMaxEpisodes"/> episodes is read in full.
/// </summary>
public static class EpisodeLookupChains
{
    public static readonly IReadOnlyDictionary<(EpisodeLookupFlow Flow, EpisodeLookupAge Age), CatalogueLookupStrategy[]> LargeShow =
        new Dictionary<(EpisodeLookupFlow, EpisodeLookupAge), CatalogueLookupStrategy[]>
        {
            // Indexer: its episodes are at the top of the feed; read newest pages past the window, never search.
            [(EpisodeLookupFlow.Index, EpisodeLookupAge.Recent)] = [CatalogueLookupStrategy.NewestPages],
            [(EpisodeLookupFlow.Index, EpisodeLookupAge.OldOrUndated)] = [CatalogueLookupStrategy.NewestPages],

            // Submit URL / categorisers.
            [(EpisodeLookupFlow.Submit, EpisodeLookupAge.Recent)] =
                [CatalogueLookupStrategy.NewestPages, CatalogueLookupStrategy.TitleSearch, CatalogueLookupStrategy.BoundedWalk],
            [(EpisodeLookupFlow.Submit, EpisodeLookupAge.OldOrUndated)] =
                [CatalogueLookupStrategy.TitleSearch, CatalogueLookupStrategy.BoundedWalk],

            // Discovery enrichers.
            [(EpisodeLookupFlow.Discovery, EpisodeLookupAge.Recent)] =
                [CatalogueLookupStrategy.NewestPages, CatalogueLookupStrategy.TitleSearch, CatalogueLookupStrategy.BoundedWalk],
            [(EpisodeLookupFlow.Discovery, EpisodeLookupAge.OldOrUndated)] =
                [CatalogueLookupStrategy.TitleSearch, CatalogueLookupStrategy.BoundedWalk],

            // EnrichExistingEpisodesFromPodcastServices CLI.
            [(EpisodeLookupFlow.Enrich, EpisodeLookupAge.Recent)] =
                [CatalogueLookupStrategy.NewestPages, CatalogueLookupStrategy.TitleSearch, CatalogueLookupStrategy.BoundedWalk],
            [(EpisodeLookupFlow.Enrich, EpisodeLookupAge.OldOrUndated)] =
                [CatalogueLookupStrategy.TitleSearch, CatalogueLookupStrategy.BoundedWalk],
        };
}
