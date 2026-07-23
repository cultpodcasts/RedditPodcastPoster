using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Homepage;

namespace Api.Handlers.Homepage;

public class PublishHomepageHandler(
    IHomepagePublishService homepagePublishService,
    ILogger<PublishHomepageHandler> logger) : IPublishHomepageHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c)
    {
        var result = await homepagePublishService.PublishAsync(c);
        return result.Status switch
        {
            HomepagePublishStatus.Ok =>
                await ctx.Ok(result.Result!.ToDto(), c),
            HomepagePublishStatus.Failed when result.Result != null =>
                await ctx.InternalError(result.Result.ToDto(), c),
            HomepagePublishStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Publish homepage failed with unexpected status.");
        return ctx.InternalError();
    }
}
