using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.People;

namespace Api.Handlers.People;

public class GetAllPeopleHandler(
    IPersonGetAllService personGetAllService,
    ILogger<GetAllPeopleHandler> logger) : IGetAllPeopleHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c)
    {
        var result = await personGetAllService.GetAllAsync(c);

        return result.Status switch
        {
            PersonGetAllStatus.Ok =>
                await ctx.Ok(result.People!.Select(x => x.ToDto()).ToList(), c),
            PersonGetAllStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve people"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("People get-all failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve people"), c);
    }
}
