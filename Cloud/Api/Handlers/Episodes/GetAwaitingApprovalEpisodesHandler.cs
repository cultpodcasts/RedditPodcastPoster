using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class GetAwaitingApprovalEpisodesHandler(
    IEpisodeAwaitingApprovalService episodeAwaitingApprovalService,
    EpisodeDtoMapper episodeDtoMapper,
    ILogger<GetAwaitingApprovalEpisodesHandler> logger) : IGetAwaitingApprovalEpisodesHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        CancellationToken c)
    {
        var result = await episodeAwaitingApprovalService.GetAwaitingApprovalAsync(c);

        return result.Status switch
        {
            EpisodeAwaitingApprovalStatus.Ok =>
                await ctx.Ok(await episodeDtoMapper.ToDtos(result.Episodes!, c), c),
            EpisodeAwaitingApprovalStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve episodes awaiting approval"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Awaiting-approval episodes get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve episodes awaiting approval"), c);
    }
}
