using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.News;

public interface IGetNewsReportHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, Guid reportId, CancellationToken c);
}
