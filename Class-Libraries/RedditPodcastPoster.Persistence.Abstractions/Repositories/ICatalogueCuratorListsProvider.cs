using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// Loads the four catalogue curator lists from LookUps, falling back to compiled seeds
/// when a document is missing or empty. Shared by identify / migrate CLIs and submit paths.
/// </summary>
public interface ICatalogueCuratorListsProvider
{
    Task<CatalogueCuratorLists> GetAsync(CancellationToken cancellationToken = default);
}
