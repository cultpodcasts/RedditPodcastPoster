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
/// <param name="Removed">The item, or its parent, is removed.</param>
public record CataloguePlayableResolution(
    string ContentKind,
    PodcastEpisode Projection,
    Guid? ParentId,
    string? ParentName,
    string Slug,
    bool Removed);

public interface ICataloguePlayableResolver
{
    /// <summary>Null when no news report, TV episode or film has the id.</summary>
    Task<CataloguePlayableResolution?> Resolve(Guid id, CancellationToken cancellationToken);
}
