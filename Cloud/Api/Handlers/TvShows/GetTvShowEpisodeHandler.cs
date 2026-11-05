using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.TvShows;

namespace Api.Handlers.TvShows;

public class GetTvShowEpisodeHandler(
    ITvShowEpisodeService tvShowEpisodeService,
    IPlayableGuestDtoEnricher guestDtoEnricher,
    ILogger<GetTvShowEpisodeHandler> logger) : IGetTvShowEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid episodeId,
        CancellationToken c)
    {
        var result = await tvShowEpisodeService.GetAsync(episodeId, c);

        return result.Status switch
        {
            TvShowEpisodeGetStatus.Found =>
                await ctx.Ok(await ToEnrichedDto(result.Episode!, c), c),
            TvShowEpisodeGetStatus.NotFound =>
                await ctx.NotFound(ApiErrorResponse.Failure("Unable to retrieve TV-show episode"), c),
            TvShowEpisodeGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve TV-show episode"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<TvShowEpisodeDto> ToEnrichedDto(
        RedditPodcastPoster.Models.TvShows.TvShowEpisode episode,
        CancellationToken c)
    {
        var dto = episode.ToDto();
        await guestDtoEnricher.EnrichAsync(
            dto,
            episode.Title,
            episode.Description,
            episode.Language,
            episode.Guests,
            cancellationToken: c);
        return dto;
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("TV-show episode get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve TV-show episode"), c);
    }
}
