using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.TitleCasingRules;

namespace Api.Handlers.TitleCasingRules;

public class GetTitleCasingRulesHandler(
    ITitleCasingRulesGetService titleCasingRulesGetService,
    ILogger<GetTitleCasingRulesHandler> logger) : IGetTitleCasingRulesHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c)
    {
        var result = await titleCasingRulesGetService.GetAllAsync(c);
        return result.Status switch
        {
            TitleCasingRulesGetStatus.Ok =>
                await ctx.Ok(
                    TitleCasingRulesResponseBuilder.BuildList(result.Documents!),
                    c),
            TitleCasingRulesGetStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Title casing rules list failed with unexpected status.");
        return ctx.InternalError();
    }
}
