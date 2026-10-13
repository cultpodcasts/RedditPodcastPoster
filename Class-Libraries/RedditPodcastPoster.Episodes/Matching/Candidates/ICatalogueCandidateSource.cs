namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// Platform adapter (Spotify, Apple, …) that supplies catalogue candidates for one episode lookup.
/// Adapters only fetch; <see cref="ICatalogueCandidatePolicy"/> decides what to fetch and in which order.
/// </summary>
public interface ICatalogueCandidateSource<TItem>
{
    /// <summary>True when the platform show (feed) for the podcast is known.</summary>
    bool HasShow { get; }

    /// <summary>Newest episodes from the show feed, newest first, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<TItem>> GetRecentShowEpisodes(int limit);

    /// <summary>Broader lookup for episodes outside the recent page (title search, full catalogue, …).</summary>
    Task<IReadOnlyList<TItem>> SearchEpisodes();

    /// <summary>Platform identity, used to avoid matching the same candidate twice.</summary>
    string GetId(TItem item);
}
