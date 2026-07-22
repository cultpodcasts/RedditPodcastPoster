using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.People;

public interface IGetPersonHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, string personName, CancellationToken c);
}
