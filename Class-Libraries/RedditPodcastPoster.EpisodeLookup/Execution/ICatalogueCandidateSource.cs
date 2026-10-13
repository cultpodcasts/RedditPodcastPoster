using RedditPodcastPoster.Models.Episodes;

using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>One page of a show's episodes, as the platform returned it.</summary>
/// <param name="Items">The page's episodes, newest first when <paramref name="IsNewestFirst"/>.</param>
/// <param name="HasMore">Whether an older page exists.</param>
/// <param name="TotalEpisodes">Show size reported by the platform on this page (Spotify <c>Paging.Total</c>), if any.</param>
/// <param name="IsNewestFirst">False for catalogues the platform only returns oldest-first; date early-stop is then off and only the page cap bounds the read.</param>
public sealed record CatalogueShowPage<TItem>(
    IReadOnlyList<TItem> Items,
    bool HasMore,
    int? TotalEpisodes = null,
    bool IsNewestFirst = true);

/// <summary>
/// Platform adapter (Spotify, Apple) for one lookup. Adapters only fetch and map; the shared planner and executor
/// decide what to fetch, in which order, and how to match. Every HTTP call an adapter makes goes through
/// <see cref="ICatalogueRequestGuard"/> and returns null (never throws) when the guard fires.
/// </summary>
public interface ICatalogueCandidateSource<TItem>
{
    /// <summary>Platform name used in logs and show-size bookkeeping ("Spotify", "Apple").</summary>
    string Platform { get; }

    /// <summary>The platform show id, when known.</summary>
    string? ShowId { get; }


    /// <summary>
    /// Cheap show-size lookup used when the podcast has no stored count: a page that would be read anyway
    /// (Spotify) or one quick catalogue lookup (Apple). Never a long query just to count.
    /// </summary>
    Task<int?> ProbeShowEpisodeCount(CancellationToken cancellationToken);

    /// <summary>Newest-first page <paramref name="pageIndex"/> (0 = newest); null when unavailable or the guard fired.</summary>
    Task<CatalogueShowPage<TItem>?> GetNewestPage(int pageIndex, CancellationToken cancellationToken);

    /// <summary>One title search scoped to the show (or to the show name when the id is unknown).</summary>
    Task<IReadOnlyList<TItem>> SearchByTitle(CancellationToken cancellationToken);

    /// <summary>Platform identity, so a candidate is matched at most once per lookup.</summary>
    string GetId(TItem item);

    /// <summary>Maps a platform item to the domain <see cref="Episode"/> the shared matcher scores.</summary>
    Episode ToCatalogueEpisode(TItem item);
}
