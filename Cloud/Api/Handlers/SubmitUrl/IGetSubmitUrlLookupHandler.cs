using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.SubmitUrl;

public interface IGetSubmitUrlLookupHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, Uri url, CancellationToken cancellationToken);
}
