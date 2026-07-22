using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.SubmitUrl;

namespace Api.Handlers.SubmitUrl;

public class PostSubmitUrlHandler(
    ISubmitUrlService submitUrlService,
    ILogger<PostSubmitUrlHandler> logger) : IPostSubmitUrlHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubmitUrlRequest submitUrlModel,
        CancellationToken c)
    {
        var result = await submitUrlService.SubmitAsync(submitUrlModel, c);
        return result.Status switch
        {
            SubmitUrlStatus.Ok =>
                await ctx.Ok(SubmitUrlResponse.Successful(result.Result!), c),
            SubmitUrlStatus.PodcastNotFound =>
                await ctx.NotFound(new { message = result.Message }, c),
            SubmitUrlStatus.Failed =>
                await ctx.InternalError(SubmitUrlResponse.Failure(result.Message ?? "Failure"), c),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Submit url failed with unexpected status.");
        return ctx.InternalError();
    }
}
