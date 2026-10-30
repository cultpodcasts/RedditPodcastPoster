using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

/// <summary>Outcome of a curator's on-demand index of one TV show or news organisation.</summary>
public enum CatalogueParentIndexStatus
{
    /// <summary>The parent was indexed; <see cref="CatalogueParentIndexResult.ChangedPlayables"/> holds what was saved.</summary>
    Indexed,

    /// <summary>No TV show or news organisation has this id.</summary>
    NotFound,

    /// <summary>The parent is removed, or its <c>indexing</c> settings do not auto-index (no index-all, no title regex).</summary>
    NotIndexable,

    /// <summary>The podcast it was migrated from (same id) still exists and is indexed instead (unfinished CatalogueMigrate).</summary>
    SourcePodcastStillExists,

    /// <summary>The per-parent time budget cut the index off. Anything saved before the cut stays saved.</summary>
    BudgetExceeded
}

/// <param name="Status">What happened.</param>
/// <param name="ChangedPlayables">
/// The TvShowEpisode / NewsReport items this index saved (new, merged or removed by elimination terms). Empty unless
/// <paramref name="Status"/> is <see cref="CatalogueParentIndexStatus.Indexed"/>.
/// </param>
public sealed record CatalogueParentIndexResult(
    CatalogueParentIndexStatus Status,
    IReadOnlyList<Playable>? ChangedPlayables = null)
{
    public IReadOnlyList<Playable> Changed => ChangedPlayables ?? [];
}
