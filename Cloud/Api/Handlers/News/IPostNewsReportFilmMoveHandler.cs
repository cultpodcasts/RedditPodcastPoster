using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.News;

public interface IPostNewsReportFilmMoveHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        NewsReportFilmMoveCommand command,
        CancellationToken cancellationToken);
}
