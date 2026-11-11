using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.News;

public interface IPostNewsReportTvShowMoveHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        NewsReportTvShowMoveCommand command,
        CancellationToken cancellationToken);
}
