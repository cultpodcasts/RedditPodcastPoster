using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;

namespace Api.Resolvers;

/// <summary>
/// A news report, TV episode or film found by its id, projected into the podcast-episode shape the episode read
/// models are built from (<see cref="RedditPodcastPoster.Models.Catalogue.CatalogueParentKindMapper"/>.ToPostProjection).
/// The projection is read-only: never save it.
/// </summary>
/// <param name="ContentKind"><c>NewsReport</c>, <c>TvShowEpisode</c> or <c>Film</c>.</param>
/// <param name="ParentId">News organisation or TV show id; null for a film (no parent).</param>
/// <param name="ParentName">News organisation or TV show name; null for a film.</param>
/// <param name="Slug">The site route segment: the parent name, or the film's own name.</param>
/// <param name="Removed">The item, or its parent, is removed (public GET hides both).</param>
/// <param name="ParentRemoved">
/// The parent publisher is removed (News organisation / TV show). Always false for Film.
/// Curator publish rejects only this — item-level <c>Removed</c> may still publish and clear.
/// </param>
public record CataloguePlayableResolution(
    string ContentKind,
    PodcastEpisode Projection,
    Guid? ParentId,
    string? ParentName,
    string Slug,
    bool Removed,
    bool ParentRemoved = false);

public interface ICataloguePlayableResolver
{
    /// <summary>
    /// Null when no news report, TV episode or film has the id.
    /// Three-container fan-out for guid-only callers (episode GET / guid-only publish fallback).
    /// Prefer <see cref="Resolve(Guid, ContentKind, CancellationToken)"/> when the route already names a kind.
    /// </summary>
    Task<CataloguePlayableResolution?> Resolve(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Hits only the repository for <paramref name="expectedKind"/> (Film / NewsReport / TvShowEpisode).
    /// Kind publish routes use this; null when that container has no row (including wrong-kind ids).
    /// </summary>
    Task<CataloguePlayableResolution?> Resolve(
        Guid id,
        ContentKind expectedKind,
        CancellationToken cancellationToken);
}
