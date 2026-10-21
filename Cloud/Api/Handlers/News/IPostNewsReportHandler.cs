using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.News;

public interface IPostNewsReportHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        NewsReportChangeRequestWrapper request,
        CancellationToken c);
}
