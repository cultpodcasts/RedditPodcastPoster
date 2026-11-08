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

    /// <summary>
    /// True when the document this patch was read from contains a <c>services</c> member.
    /// An explicit JSON null counts as defined. Remove is emitted only when
    /// <see cref="Services"/> is null and this is true (#1154).
    /// </summary>
    public bool ServicesDefined { get; init; }

    public bool SetYouTubeId { get; init; }
    public string? YouTubeId { get; init; }

    /// <summary>
    /// True when the document this patch was read from contains a <c>youTubeId</c> member.
    /// An explicit JSON null counts as defined. Remove is emitted only when
    /// <see cref="YouTubeId"/> is null and this is true (#1154).
    /// </summary>
    public bool YouTubeIdDefined { get; init; }

    public bool IsEmpty => !SetServices && !SetYouTubeId;
}

/// <summary>
/// A stored item, the Cosmos ETag it was read with, and whether <c>services</c> and <c>youTubeId</c>
/// were present on the raw document. A typed model cannot tell an omitted member from an explicit JSON null;
/// a stored null still counts as defined (#1154).
/// </summary>
public sealed record ETagged<T>(T Item, string ETag, bool ServicesDefined = false, bool YouTubeIdDefined = false);

public enum CataloguePlayablePatchOutcome
{
    Patched,

    /// <summary>The document no longer exists (deleted or moved). Nothing was written; a patch never upserts.</summary>
    NotFound,

    /// <summary>The document changed after it was read (ETag mismatch, HTTP 412). Nothing was written.</summary>
    Conflict
}
