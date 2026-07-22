using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Terms;

public interface IPostTermsHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TermSubmitRequest termSubmitRequest,
        CancellationToken c);
}
