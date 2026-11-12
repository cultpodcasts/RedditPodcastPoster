using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.News;

public interface IPostNewsReportOrganisationMoveHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        NewsReportOrganisationMoveCommand command,
        CancellationToken cancellationToken);
}
