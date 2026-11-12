using Api.Models;

namespace Api.Services.News;

public interface INewsReportOrganisationMoveService
{
    Task<NewsReportOrganisationMoveResult> MoveAsync(
        NewsReportOrganisationMoveCommand command,
        CancellationToken cancellationToken);
}
