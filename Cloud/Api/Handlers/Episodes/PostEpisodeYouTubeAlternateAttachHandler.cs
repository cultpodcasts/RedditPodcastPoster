using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class PostEpisodeYouTubeAlternateAttachHandler(
    IYouTubeAlternateAttachService youTubeAlternateAttachService,
    ILogger<PostEpisodeYouTubeAlternateAttachHandler> logger) : IPostEpisodeYouTubeAlternateAttachHandler
{
    private const string FailureMessage = "Unable to attach the episode as a YouTube alternate";

    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeYouTubeAlternateAttachCommand command,
        CancellationToken cancellationToken)
    {
        var result = await youTubeAlternateAttachService.AttachAsync(command, cancellationToken);
        return result.Status switch
        {
            EpisodeYouTubeAlternateAttachStatus.Accepted =>
                await ctx.Accepted(result.ToDto(), cancellationToken),
            EpisodeYouTubeAlternateAttachStatus.NotFound =>
                await ctx.NotFound(
                    new
                    {
                        podcastId = command.PodcastId,
                        episodeId = command.EpisodeId,
                        targetId = command.Request.TargetId,
                        error = result.Error
                    },
                    cancellationToken),
            EpisodeYouTubeAlternateAttachStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Invalid request"), cancellationToken),
            EpisodeYouTubeAlternateAttachStatus.Conflict =>
                await ctx.Conflict(result.ToDto(), cancellationToken),
            EpisodeYouTubeAlternateAttachStatus.Failed =>
                await ctx.InternalError(result.ToDto(), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("YouTube-alternate attach failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure(FailureMessage), cancellationToken);
    }
}
