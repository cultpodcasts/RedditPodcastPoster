using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Podcasts;

namespace Api.Handlers.Podcasts;

public class IndexPodcastHandler(
    IPodcastIndexService podcastIndexService,
    ILogger<IndexPodcastHandler> logger) : IIndexPodcastHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string podcastName,
        CancellationToken c)
    {
        var result = await podcastIndexService.IndexAsync(podcastName, c);

        return result.Status switch
        {
            PodcastIndexStatus.Ok =>
                await ctx.Ok(ToResponse(result), c),
            PodcastIndexStatus.NotFound =>
                await ctx.NotFound(ToResponse(result), c),
            PodcastIndexStatus.BadRequest =>
                await ctx.BadRequest(ToResponse(result), c),
            PodcastIndexStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to index podcast"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private static IndexPodcastResponse ToResponse(PodcastIndexResult result)
    {
        var indexed = result.SearchIndexer?.ToDto() ?? SearchIndexerState.Unknown;
        return result.IndexResponse!.ToDto(indexed);
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Podcast index failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to index podcast"), c);
    }
}
