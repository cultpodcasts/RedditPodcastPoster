using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.SubmitUrl;

public interface IPostSubmitUrlExtractHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubmitUrlExtractRequest request,
        CancellationToken c);
}
