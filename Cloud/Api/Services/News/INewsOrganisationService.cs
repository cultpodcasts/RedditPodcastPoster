using Api.Models;

namespace Api.Services.News;

public interface INewsOrganisationService
{
    Task<NewsOrganisationGetResult> GetAsync(string identifier, CancellationToken cancellationToken);

    Task<NewsOrganisationUpdateResult> UpdateAsync(
        NewsOrganisationChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
