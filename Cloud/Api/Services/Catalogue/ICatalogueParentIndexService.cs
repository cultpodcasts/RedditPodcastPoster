using Api.Models;

namespace Api.Services.Catalogue;

public interface ICatalogueParentIndexService
{
    /// <param name="identifier">The TV show's guid or (unique, case-insensitive) name.</param>
    Task<CatalogueIndexResult> IndexTvShow(string identifier, CancellationToken cancellationToken);

    /// <param name="identifier">The news organisation's guid or (unique, case-insensitive) name.</param>
    Task<CatalogueIndexResult> IndexNewsOrganisation(string identifier, CancellationToken cancellationToken);
}
