using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

/// <summary>Outcome of a curator's on-demand index of one TV show or news organisation.</summary>
public enum CatalogueParentIndexStatus
{
    /// <summary>
    /// The parent was indexed; <see cref="CatalogueParentIndexResult.ChangedPlayables"/> holds what was saved. When
    /// <see cref="CatalogueParentIndexResult.EnrichOnly"/> is set, only Spotify / Apple links were filled in.
    /// </summary>
    Indexed,

    /// <summary>
    /// Nothing could be done: the parent is removed, or it is not auto-indexed and has no Spotify or Apple id to
    /// enrich from. <see cref="CatalogueParentIndexResult.NotIndexableReason"/> says which.
    /// </summary>
    NotIndexable,

    /// <summary>The podcast it was migrated from (same id) still exists and is indexed instead (unfinished kind transfer).</summary>
    SourcePodcastStillExists,

    /// <summary>
    /// The on-demand time budget cut the index off. What, if anything, was saved is not known: new items are saved in one
    /// batch near the end, and an update that ignored cancellation may still finish. Anything that was saved reaches
    /// search on the next pull-indexer run.
    /// </summary>
    TimedOut,

    /// <summary>Another on-demand or hourly index already holds this parent's indexing lease (HTTP 409).</summary>
    AlreadyRunning,

    /// <summary>
    /// The parent is not auto-indexed, so an enrich-only pass ran (#1400), but no recent item gained a Spotify or Apple
    /// link: none was missing one, or none could be matched. Nothing was saved.
    /// </summary>
    NothingToEnrich
}

/// <summary>Why a parent is <see cref="CatalogueParentIndexStatus.NotIndexable"/>.</summary>
public enum CatalogueParentNotIndexableReason
{
    /// <summary>The parent is removed.</summary>
    Removed,

    /// <summary>
    /// The parent is not auto-indexed (no index-all, no title regex) and has no Spotify or Apple id, so there is no
    /// discovery and nothing to enrich from.
    /// </summary>
    NotAutoIndexed
}

/// <param name="Status">What happened.</param>
/// <param name="ChangedPlayables">
/// The TvShowEpisode / NewsReport items this index saved (new, merged, enriched or removed by elimination terms). Empty
/// unless <paramref name="Status"/> is <see cref="CatalogueParentIndexStatus.Indexed"/>.
/// </param>
/// <param name="NotIndexableReason">Set when <paramref name="Status"/> is <see cref="CatalogueParentIndexStatus.NotIndexable"/>.</param>
/// <param name="EnrichOnly">
/// True when the parent is not auto-indexed and the index only filled in missing Spotify / Apple links (no discovery,
/// <c>lastIndexed</c> and <c>indexing</c> untouched). <paramref name="ChangedPlayables"/> are then the enriched items.
/// </param>
public sealed record CatalogueParentIndexResult(
    CatalogueParentIndexStatus Status,
    IReadOnlyList<Playable>? ChangedPlayables = null,
    CatalogueParentNotIndexableReason? NotIndexableReason = null,
    bool EnrichOnly = false)
{
    public IReadOnlyList<Playable> Changed => ChangedPlayables ?? [];
}