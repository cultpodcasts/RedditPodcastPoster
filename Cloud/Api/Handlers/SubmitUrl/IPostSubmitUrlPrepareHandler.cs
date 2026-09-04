using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.SubmitUrl;

public interface IPostSubmitUrlPrepareHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubmitUrlPrepareRequest request,
        CancellationToken c);
}
