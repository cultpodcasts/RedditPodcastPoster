using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>Result of one catalogue lookup.</summary>
/// <param name="Match">The matched platform item, or null for a clean miss.</param>
/// <param name="Stage">Stage that matched, or <see cref="CatalogueLookupExecutor.StageMiss"/>.</param>
/// <param name="FromSearch">True when the match came from a title search (resolvers may hydrate it).</param>
public sealed record CatalogueLookupResult<TItem>(TItem? Match, string Stage, bool FromSearch = false)
    where TItem : class;

/// <summary>
/// Runs the planned strategy chain for one lookup against a platform source and matches each step's new
/// candidates with the shared <see cref="ICatalogueEpisodeMatcher"/>. Identical for every platform.
/// </summary>
public interface ICatalogueLookupExecutor
{
    Task<CatalogueLookupResult<TItem>> FindMatch<TItem>(
        CatalogueLookup lookup,
        ICatalogueCandidateSource<TItem> source,
        Func<TItem, bool>? reducer = null,
        CancellationToken cancellationToken = default)
        where TItem : class;
}
