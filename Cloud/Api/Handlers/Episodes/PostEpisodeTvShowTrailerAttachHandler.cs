using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class PostEpisodeTvShowTrailerAttachHandler(
    IEpisodeTvShowTrailerAttachService episodeTvShowTrailerAttachService,
    ILogger<PostEpisodeTvShowTrailerAttachHandler> logger) : IPostEpisodeTvShowTrailerAttachHandler
{
    private const string FailureMessage = "Unable to attach episode as a TV show trailer";

    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeTvShowTrailerAttachCommand command,
        CancellationToken cancellationToken)
    {
        var result = await episodeTvShowTrailerAttachService.AttachAsync(command, cancellationToken);
        return result.Status switch
        {
            EpisodeTvShowTrailerAttachStatus.Accepted =>
                await ctx.Accepted(result.ToDto(), cancellationToken),
            EpisodeTvShowTrailerAttachStatus.NotFound =>
                await ctx.NotFound(
                    new
                    {
                        podcastId = command.PodcastId,
                        episodeId = command.EpisodeId,
                        tvShowId = command.Request.TvShowId,
                        error = result.Error
                    },
                    cancellationToken),
            EpisodeTvShowTrailerAttachStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Invalid request"), cancellationToken),
            EpisodeTvShowTrailerAttachStatus.Conflict =>
                await ctx.Conflict(result.ToDto(), cancellationToken),
            EpisodeTvShowTrailerAttachStatus.Failed =>
                // Include ToDto so failureIndexing reaches the client (docs/tv-show-trailers.md).
                await ctx.InternalError(result.ToDto(), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("Episode TV show trailer attach failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure(FailureMessage), cancellationToken);
    }
}

