using RedditPodcastPoster.EntitySearchIndexer.Models;

namespace Api.Models;

/// <summary>Mirrors <see cref="PodcastRenameStatus"/>. There is no TooMany: exactly one entity is renamed.</summary>
public enum CatalogueRenameStatus
{
    Ok,
    Conflict,
    NotFound,
    BadRequest,
    InvalidName,
    Failed
}

public record CatalogueRenameResult(
    CatalogueRenameStatus Status,
    EntitySearchIndexerResponse? SearchIndexer = null,
    Guid[]? AmbiguousIds = null);
