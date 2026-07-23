using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.People;

namespace Api.Handlers.People;

public class GetPersonHandler(
    IPersonGetService personGetService,
    ILogger<GetPersonHandler> logger) : IGetPersonHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string personName,
        CancellationToken c)
    {
        var result = await personGetService.GetAsync(personName, c);

        return result.Status switch
        {
            PersonGetStatus.Ok =>
                await ctx.Ok(result.Person!.ToDto(), c),
            PersonGetStatus.NotFound =>
                ctx.NotFound(),
            PersonGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve person"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Person get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve person"), c);
    }
}
