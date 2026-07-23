using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class GetOutgoingEpisodesHandler(
    IEpisodeOutgoingService episodeOutgoingService,
    EpisodeDtoMapper episodeDtoMapper,
    ILogger<GetOutgoingEpisodesHandler> logger) : IGetOutgoingEpisodesHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        CancellationToken c)
    {
        var result = await episodeOutgoingService.GetOutgoingAsync(
            ctx.Query("days"),
            ctx.Query("posted"),
            ctx.Query("tweeted"),
            ctx.Query("blueskyPosted"),
            c);

        return result.Status switch
        {
            EpisodeOutgoingStatus.Ok =>
                await ctx.Ok(await episodeDtoMapper.ToDtos(result.Episodes!, c), c),
            EpisodeOutgoingStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve out-going episodes"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Outgoing episodes get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve out-going episodes"), c);
    }
}
