using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.People;

namespace Api.Handlers.People;

public class PutPersonHandler(
    IPersonCreateService personCreateService,
    ILogger<PutPersonHandler> logger) : IPutPersonHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PersonChangeRequest person,
        CancellationToken ct)
    {
        var result = await personCreateService.CreateAsync(person, ct);

        return result.Status switch
        {
            PersonCreateStatus.Accepted =>
                await ctx.Accepted(result.Person!.ToDto(), ct),
            PersonCreateStatus.BadRequest =>
                await ctx.BadRequest(new { message = result.Message }, ct),
            PersonCreateStatus.Conflict =>
                await ctx.Conflict(new { conflict = result.ConflictName }, ct),
            PersonCreateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to create person"), ct),
            _ => await LogAndFail(ctx, ct)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Person create failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to create person"), c);
    }
}
