using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Models;

namespace Api.Handlers.Discovery;

public interface IPostDiscoveryCurationHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        DiscoverySubmitRequest model,
        CancellationToken c);
}
