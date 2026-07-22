using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Podcasts;

namespace Api.Handlers.Podcasts;

public class RenamePodcastHandler(
    IPodcastRenameService podcastRenameService,
    ILogger<RenamePodcastHandler> logger) : IRenamePodcastHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastRenameCommand change,
        CancellationToken c)
    {
        var result = await podcastRenameService.RenameAsync(change, c);

        return result.Status switch
        {
            PodcastRenameStatus.Ok =>
                await ctx.Ok(result.SearchIndexer!.ToPodcastRenameResponse(), c),
            PodcastRenameStatus.Conflict =>
                ctx.Conflict(),
            PodcastRenameStatus.NotFound =>
                ctx.NotFound(),
            PodcastRenameStatus.BadRequest =>
                ctx.BadRequest(),
            PodcastRenameStatus.InvalidName =>
                ctx.InternalError(),
            PodcastRenameStatus.TooMany =>
                ctx.InternalError(),
            PodcastRenameStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to rename podcast"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Podcast rename failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to rename podcast"), c);
    }
}
