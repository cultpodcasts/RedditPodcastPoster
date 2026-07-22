using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.People;

public interface IGetAllPeopleHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c);
}
