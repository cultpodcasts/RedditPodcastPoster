using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// Platform adapter (Spotify, Apple, …) that supplies catalogue candidates for one lookup.
/// Adapters only fetch and map; <see cref="ICatalogueCandidatePolicy"/> decides what to fetch, in which order,
/// and how to match.
/// </summary>
public interface ICatalogueCandidateSource<TItem>
{
    /// <summary>Platform name used in logs (e.g. "Spotify", "Apple").</summary>
    string Platform { get; }

    /// <summary>True when the platform show (feed) for the podcast is known.</summary>
    bool HasShow { get; }

    /// <summary>
    /// At least the newest <paramref name="pageSize"/> episodes of the show feed, newest first.
    /// An adapter whose feed is already one cached fetch (Apple) may return the whole feed so matching runs in one pass.
    /// </summary>
    Task<IReadOnlyList<TItem>> GetRecentShowEpisodes(int pageSize, CancellationToken cancellationToken);

    /// <summary>Broader lookup for episodes outside the recent page (title search, full catalogue, …).</summary>
    Task<IReadOnlyList<TItem>> SearchEpisodes(CancellationToken cancellationToken);

    /// <summary>Platform identity, used so a candidate is matched at most once per lookup.</summary>
    string GetId(TItem item);

    /// <summary>Maps a platform item to the domain <see cref="Episode"/> the shared matcher scores.</summary>
    Episode ToCatalogueEpisode(TItem item);
}
