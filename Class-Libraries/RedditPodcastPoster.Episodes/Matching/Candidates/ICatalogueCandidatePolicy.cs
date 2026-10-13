namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// Shared, platform-agnostic order in which catalogue candidates are sourced before matching.
/// </summary>
public interface ICatalogueCandidatePolicy
{
    /// <summary>
    /// Sources candidates from <paramref name="source"/> and returns the first non-null result of
    /// <paramref name="match"/> (normally the shared <see cref="IEpisodePlatformMatcher"/>).
    /// </summary>
    Task<TItem?> FindMatch<TItem>(
        DateTime? released,
        ICatalogueCandidateSource<TItem> source,
        Func<IReadOnlyList<TItem>, Task<TItem?>> match)
        where TItem : class;
}
