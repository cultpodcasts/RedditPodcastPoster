using RedditPodcastPoster.Models.News;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface INewsOrganisationRepository : IRepository<NewsOrganisation>, IFilterableRepository<NewsOrganisation>
{
    Task<NewsOrganisation?> GetNewsOrganisation(Guid newsOrganisationId);
    Task Delete(Guid newsOrganisationId);

    /// <summary>
    /// Patch only the Indexer-owned fields (<see cref="CatalogueParentIndexerPatch"/>), so a curator edit,
    /// rename or removal saved during an index pass is not overwritten. Returns false when the NewsOrganisation no
    /// longer exists.
    /// </summary>
    Task<bool> PatchIndexerState(Guid newsOrganisationId, CatalogueParentIndexerPatch patch);
}
