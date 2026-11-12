using Api.Models;

namespace Api.Services.News;

public interface INewsOrganisationDeleteService
{
    Task<NewsOrganisationDeleteResult> DeleteAsync(Guid newsOrganisationId, CancellationToken cancellationToken);
}
