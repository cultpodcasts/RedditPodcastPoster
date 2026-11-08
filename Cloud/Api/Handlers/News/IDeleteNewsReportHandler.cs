using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.News;

public interface IDeleteNewsReportHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid reportId,
        CancellationToken c);
}
