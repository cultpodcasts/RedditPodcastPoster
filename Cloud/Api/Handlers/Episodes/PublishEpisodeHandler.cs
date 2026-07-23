using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class PublishEpisodeHandler(
    IEpisodePublishService episodePublishService,
    ILogger<PublishEpisodeHandler> logger) : IPublishEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodePublishRequestWrapper publishRequest,
        CancellationToken c)
    {
        var result = await episodePublishService.PublishAsync(publishRequest, c);

        return result.Status switch
        {
            EpisodePublishStatus.Ok =>
                await ctx.Ok(result.Outcome!.ToDto(), c),
            EpisodePublishStatus.BadRequest =>
                await ctx.BadRequest(result.Outcome!.ToDto(), c),
            EpisodePublishStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Episode publish failed with unexpected status.");
        return ctx.InternalError();
    }
}
