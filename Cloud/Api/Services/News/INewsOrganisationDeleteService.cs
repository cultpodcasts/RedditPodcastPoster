using Api.Models;

namespace Api.Services.News;

public interface INewsOrganisationDeleteService
{
    /// <param name="newsOrganisationId">The empty organisation to delete.</param>
    /// <param name="redirectToNewsOrganisationId">
    /// Optional: the organisation its reports went to. When its name differs, the old name route is redirected to it.
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<NewsOrganisationDeleteResult> DeleteAsync(
        Guid newsOrganisationId,
        Guid? redirectToNewsOrganisationId,
        CancellationToken cancellationToken);
}