using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.SubmitUrl;

namespace Api.Handlers.SubmitUrl;

public class PostSubmitUrlHandler(
    ISubmitUrlService submitUrlService,
    ILogger<PostSubmitUrlHandler> logger) : IPostSubmitUrlHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubmitUrlRequest submitUrlModel,
        CancellationToken c)
    {
        var result = await submitUrlService.SubmitAsync(submitUrlModel, c);
        return result.Status switch
        {
            SubmitUrlStatus.Ok =>
                await ctx.Ok(SubmitUrlResponse.Successful(result.Result!), c),
            SubmitUrlStatus.PodcastNotFound =>
                await ctx.NotFound(new { message = result.Message }, c),
            SubmitUrlStatus.Rejected =>
                await ctx.BadRequest(new SubmitDispositionResponse(result.Result?.ContentKind, rejected: true), c),
            SubmitUrlStatus.RequiresCurator =>
                await ctx.Json(
                    HttpStatusCode.UnprocessableEntity,
                    new SubmitDispositionResponse(result.Result?.ContentKind, requiresCurator: true),
                    c),
            SubmitUrlStatus.Conflict when result.ContentKind != null && result.AmbiguousPodcasts != null =>
                await ctx.Conflict(
                    new AmbiguousParentConflict(result.ContentKind, result.ParentName, result.AmbiguousPodcasts),
                    c),
            SubmitUrlStatus.Conflict when result.AmbiguousPodcasts != null =>
                await ctx.Conflict(result.AmbiguousPodcasts, c),
            SubmitUrlStatus.Conflict =>
                await ctx.Conflict(ApiErrorResponse.Failure("Podcast name is ambiguous"), c),
            SubmitUrlStatus.Failed =>
                await ctx.InternalError(SubmitUrlResponse.Failure(result.Message ?? "Failure"), c),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Submit url failed with unexpected status.");
        return ctx.InternalError();
    }
}
