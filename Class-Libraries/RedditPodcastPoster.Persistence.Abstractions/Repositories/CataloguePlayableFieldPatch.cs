using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// Partial write for a TvShowEpisode / NewsReport: only <c>/services</c> and/or <c>/youTubeId</c>, so every other
/// field a concurrent writer changed survives. Used by backfills instead of a whole-document upsert.
/// </summary>
public sealed record CataloguePlayableFieldPatch
{
    public bool SetServices { get; init; }
    public Dictionary<string, ServiceLink>? Services { get; init; }
    public bool SetYouTubeId { get; init; }
    public string? YouTubeId { get; init; }
    public bool IsEmpty => !SetServices && !SetYouTubeId;
}

/// <summary>A stored item and the Cosmos ETag it was read with.</summary>
public sealed record ETagged<T>(T Item, string ETag);

public enum CataloguePlayablePatchOutcome
{
    Patched,

    /// <summary>The document no longer exists (deleted or moved). Nothing was written; a patch never upserts.</summary>
    NotFound,

    /// <summary>The document changed after it was read (ETag mismatch, HTTP 412). Nothing was written.</summary>
    Conflict
}
