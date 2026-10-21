using Api.Models;

namespace Api.Services.Catalogue;

public interface ICatalogueRenameService
{
    Task<CatalogueRenameResult> RenameNewsOrganisation(CatalogueRenameCommand command, CancellationToken cancellationToken);

    Task<CatalogueRenameResult> RenameTvShow(CatalogueRenameCommand command, CancellationToken cancellationToken);

    Task<CatalogueRenameResult> RenameFilm(CatalogueRenameCommand command, CancellationToken cancellationToken);
}
