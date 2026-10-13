namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// Shared, platform-agnostic catalogue lookup: candidate sourcing order, subject classification and
/// matching strategy are the same for every audio platform.
/// </summary>
public interface ICatalogueCandidatePolicy
{
    Task<TItem?> FindMatch<TItem>(
        CatalogueLookup lookup,
        ICatalogueCandidateSource<TItem> source,
        Func<TItem, bool>? reducer = null,
        CancellationToken cancellationToken = default)
        where TItem : class;
}
