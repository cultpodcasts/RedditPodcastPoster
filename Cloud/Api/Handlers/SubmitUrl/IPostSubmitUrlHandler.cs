using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.SubmitUrl;

public interface IPostSubmitUrlHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubmitUrlRequest submitUrlModel,
        CancellationToken c);
}
