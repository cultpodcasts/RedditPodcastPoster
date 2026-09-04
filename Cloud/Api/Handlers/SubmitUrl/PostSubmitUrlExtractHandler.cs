using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.SubmitUrl;

namespace Api.Handlers.SubmitUrl;

public class PostSubmitUrlExtractHandler(
    ISubmitUrlPrepareService submitUrlPrepareService,
    ILogger<PostSubmitUrlExtractHandler> logger) : IPostSubmitUrlExtractHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubmitUrlExtractRequest request,
        CancellationToken c)
    {
        var result = await submitUrlPrepareService.ExtractAsync(request.Url, request.Html, c);
        return result.Status switch
        {
            SubmitUrlPrepareStatus.Ok =>
                await ctx.Ok(result.Response!, c),
            SubmitUrlPrepareStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Message ?? "Bad request"), c),
            SubmitUrlPrepareStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure(result.Message ?? "Failure"), c),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Submit url extract failed with unexpected status.");
        return ctx.InternalError();
    }
}
