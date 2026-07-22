using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.People;

namespace Api.Handlers.People;

public class PostPersonHandler(
    IPersonUpdateService personUpdateService,
    ILogger<PostPersonHandler> logger) : IPostPersonHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PersonChangeRequestWrapper request,
        CancellationToken c)
    {
        var result = await personUpdateService.UpdateAsync(request, c);

        return result.Status switch
        {
            PersonUpdateStatus.Accepted =>
                ctx.Accepted(),
            PersonUpdateStatus.NotFound =>
                ctx.NotFound(),
            PersonUpdateStatus.BadRequest =>
                await ctx.BadRequest(new { message = result.Message }, c),
            PersonUpdateStatus.Conflict =>
                await ctx.Conflict(new { conflict = result.ConflictName }, c),
            PersonUpdateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to update person"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Person update failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to update person"), c);
    }
}
