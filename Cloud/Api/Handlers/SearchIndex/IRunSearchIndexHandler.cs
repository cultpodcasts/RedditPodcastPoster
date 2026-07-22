using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.SearchIndex;

public interface IRunSearchIndexHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c);
}
