using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class GetEpisodeHandler(
    IEpisodeGetService episodeGetService,
    EpisodeDtoMapper episodeDtoMapper,
    ILogger<GetEpisodeHandler> logger) : IGetEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        CancellationToken c)
    {
        var result = await episodeGetService.GetAsync(podcastEpisodeRequestWrapper, c);

        return result.Status switch
        {
            EpisodeGetStatus.Ok =>
                await ctx.Ok(
                    await episodeDtoMapper.ToDto(
                        result.Episode!,
                        result.Podcast!,
                        result.Subjects!,
                        includeGuestSuggestions: true),
                    c),
            EpisodeGetStatus.EpisodeNotFound =>
                await ctx.NotFound(new { message = "Episode not found." }, c),
            EpisodeGetStatus.PodcastNotFound =>
                await ctx.NotFound(new { message = "Podcast not found." }, c),
            EpisodeGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve episode"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Episode get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve episode"), c);
    }
}
