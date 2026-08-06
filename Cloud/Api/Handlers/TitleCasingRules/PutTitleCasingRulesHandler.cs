using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.TitleCasingRules;

namespace Api.Handlers.TitleCasingRules;

public class PutTitleCasingRulesHandler(
    ITitleCasingRulesUpdateService titleCasingRulesUpdateService,
    ILogger<PutTitleCasingRulesHandler> logger) : IPutTitleCasingRulesHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TitleCasingRulesLanguageUpdate body,
        CancellationToken c)
    {
        var result = await titleCasingRulesUpdateService.UpdateAsync(body.Language, body.Request, c);
        return result.Status switch
        {
            TitleCasingRulesUpdateStatus.Ok =>
                await ctx.Ok(
                    TitleCasingRulesResponseBuilder.Build(result.Document!, isDefault: false),
                    c),
            TitleCasingRulesUpdateStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Bad request"), c),
            TitleCasingRulesUpdateStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Title casing rules put failed with unexpected status.");
        return ctx.InternalError();
    }
}
