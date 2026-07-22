using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Public;

namespace Api.Handlers.Public;

public class GetPublicEpisodeHandler(
    IPublicEpisodeGetService publicEpisodeGetService,
    ILogger<GetPublicEpisodeHandler> logger) : IGetPublicEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        CancellationToken c)
    {
        var result = await publicEpisodeGetService.GetAsync(podcastEpisodeRequestWrapper, c);
        return result.Status switch
        {
            PublicEpisodeGetStatus.Ok =>
                await ctx.Ok(result.Episode!.ToDto(result.Podcast!), c),
            PublicEpisodeGetStatus.NotFound =>
                ctx.NotFound(),
            PublicEpisodeGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve episode"), c),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Public episode get failed with unexpected status.");
        return ctx.InternalError();
    }
}
