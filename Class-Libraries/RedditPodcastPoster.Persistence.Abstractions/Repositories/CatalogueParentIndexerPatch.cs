using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// The fields the hourly Indexer owns on a <see cref="Models.TvShows.TvShow"/> or
/// <see cref="Models.News.NewsOrganisation"/>: <c>lastIndexed</c>, <c>latestReleased</c> and the
/// <c>indexing</c> members a pass changed. Everything else on the parent (name, description, publisher,
/// removed, social handles) belongs to curators, so the Indexer patches only these and never upserts the
/// whole document from the snapshot it read at the start of the pass.
/// </summary>
/// <param name="LastIndexed">New <c>lastIndexed</c>, or null when the pass did not change it.</param>
/// <param name="LatestReleased">New <c>latestReleased</c>, or null when the pass did not change it.</param>
/// <param name="Indexing">The parent's indexing settings after the pass (values are read from here).</param>
/// <param name="ChangedIndexingProperties">JSON names of the <c>indexing</c> members the pass changed.</param>
public sealed record CatalogueParentIndexerPatch(
    DateTime? LastIndexed,
    DateTime? LatestReleased,
    CatalogueIndexingSource Indexing,
    IReadOnlyCollection<string> ChangedIndexingProperties)
{
    /// <summary>True when the pass changed none of the Indexer-owned fields, so nothing is written.</summary>
    public bool IsEmpty => LastIndexed is null && LatestReleased is null && ChangedIndexingProperties.Count == 0;
}
