using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Api.Models;

public enum CatalogueIndexStatus
{
    Ok,
    NotFound,
    /// <summary>More than one TV show / news organisation has the name.</summary>
    Conflict,
    /// <summary>
    /// Removed, or not auto-indexed with no Spotify or Apple id to enrich from (<see cref="CatalogueIndexResult.NotIndexableReason"/>).
    /// </summary>
    NotIndexable,
    /// <summary>The podcast it was migrated from still exists and is indexed instead.</summary>
    SourcePodcastStillExists,
    /// <summary>The on-demand time budget cut the index off (504; what was saved is not known).</summary>
    TimedOut,
    /// <summary>Another index already holds this parent's lease (HTTP 409).</summary>
    AlreadyRunning,
    /// <summary>Not auto-indexed, so only Spotify / Apple links were looked up, and no recent item gained one (#1400).</summary>
    NothingToEnrich,
    /// <summary>Not auto-indexed; the Spotify / Apple lookups did not finish and nothing was saved. Retryable (#1400).</summary>
    EnrichFailed,
    Failed
}

/// <param name="IndexedIds">Ids of the TV episodes / news reports the index saved (new, merged or term-removed).</param>
/// <param name="AmbiguousIds">For <see cref="CatalogueIndexStatus.Conflict"/>: the ids sharing the name.</param>
/// <param name="NotIndexableReason">For <see cref="CatalogueIndexStatus.NotIndexable"/>: removed or not auto-indexed.</param>
/// <param name="EnrichOnly">
/// For <see cref="CatalogueIndexStatus.Ok"/>: the parent is not auto-indexed, so only Spotify / Apple links were filled
/// in and <paramref name="IndexedIds"/> are the enriched items.
/// </param>
public record CatalogueIndexResult(
    CatalogueIndexStatus Status,
    IReadOnlyList<Guid>? IndexedIds = null,
    EntitySearchIndexerResponse? SearchIndexer = null,
    Guid[]? AmbiguousIds = null,
    CatalogueParentNotIndexableReason? NotIndexableReason = null,
    bool EnrichOnly = false);
