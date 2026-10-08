using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos.Extensions;
using Api.Services.SubmitUrl;

namespace Api.Handlers.SubmitUrl;

public class GetSubmitUrlLookupHandler(ISubmitUrlLookupService submitUrlLookupService)
    : IGetSubmitUrlLookupHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Uri url,
        CancellationToken cancellationToken)
    {
        var body = await submitUrlLookupService.LookupAsync(url, cancellationToken);
        return await ctx.Ok(body.ToDto(), cancellationToken);
    }
}
