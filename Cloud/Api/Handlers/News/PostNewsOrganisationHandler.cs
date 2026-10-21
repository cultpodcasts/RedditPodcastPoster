using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

public class PostNewsOrganisationHandler(
    INewsOrganisationService newsOrganisationService,
    ILogger<PostNewsOrganisationHandler> logger) : IPostNewsOrganisationHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        NewsOrganisationChangeRequestWrapper request,
        CancellationToken c)
    {
        var result = await newsOrganisationService.UpdateAsync(request, c);

        return result.Status switch
        {
            NewsOrganisationUpdateStatus.Accepted =>
                ctx.Accepted(),
            NewsOrganisationUpdateStatus.NotFound =>
                await ctx.NotFound(new { id = request.NewsOrganisationId }, c),
            NewsOrganisationUpdateStatus.BadRequest =>
                await ctx.BadRequest(new { message = result.Message }, c),
            NewsOrganisationUpdateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to update news organisation"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("News organisation update failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to update news organisation"), c);
    }
}
