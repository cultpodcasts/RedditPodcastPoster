using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.People;

public interface IPostPersonHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PersonChangeRequestWrapper request,
        CancellationToken c);
}
