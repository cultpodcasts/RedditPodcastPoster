using RedditPodcastPoster.EntitySearchIndexer.Models;

namespace Api.Models;

public enum CatalogueIndexStatus
{
    Ok,
    NotFound,
    /// <summary>More than one TV show / news organisation has the name.</summary>
    Conflict,
    /// <summary>Removed, or its indexing settings do not auto-index.</summary>
    NotIndexable,
    /// <summary>The podcast it was migrated from still exists and is indexed instead.</summary>
    SourcePodcastStillExists,
    /// <summary>The on-demand time budget cut the index off (504; what was saved is not known).</summary>
    TimedOut,
    Failed
}

/// <param name="IndexedIds">Ids of the TV episodes / news reports the index saved (new, merged or term-removed).</param>
/// <param name="AmbiguousIds">For <see cref="CatalogueIndexStatus.Conflict"/>: the ids sharing the name.</param>
public record CatalogueIndexResult(
    CatalogueIndexStatus Status,
    IReadOnlyList<Guid>? IndexedIds = null,
    EntitySearchIndexerResponse? SearchIndexer = null,
    Guid[]? AmbiguousIds = null);
