namespace Indexer.Activities;

/// <param name="PodcastIdBatches">Auto-indexed podcast ids per pass.</param>
/// <param name="CatalogueParentIdBatches">
/// Auto-indexed TvShow / NewsOrganisation ids per pass (same split), or null from older runs.
/// </param>
public record IndexIdProviderResponse(Guid[][] PodcastIdBatches, Guid[][]? CatalogueParentIdBatches = null);
