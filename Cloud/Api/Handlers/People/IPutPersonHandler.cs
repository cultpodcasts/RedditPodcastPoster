using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.People;

public interface IPutPersonHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, PersonChangeRequest person, CancellationToken ct);
}
