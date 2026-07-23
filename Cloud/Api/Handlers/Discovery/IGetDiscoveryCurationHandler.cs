using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Discovery;

public interface IGetDiscoveryCurationHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c);
}
