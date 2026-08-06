using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.TitleCasingRules;

namespace Api.Handlers.TitleCasingRules;

public class GetTitleCasingRulesByLanguageHandler(
    ITitleCasingRulesGetService titleCasingRulesGetService,
    ILogger<GetTitleCasingRulesByLanguageHandler> logger) : IGetTitleCasingRulesByLanguageHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string language,
        CancellationToken c)
    {
        var result = await titleCasingRulesGetService.GetAsync(language, c);
        return result.Status switch
        {
            TitleCasingRulesGetStatus.Ok =>
                await ctx.Ok(
                    TitleCasingRulesResponseBuilder.Build(result.Document!, result.IsDefault),
                    c),
            TitleCasingRulesGetStatus.NotFound =>
                ctx.NotFound(),
            TitleCasingRulesGetStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Title casing rules get-by-language failed with unexpected status.");
        return ctx.InternalError();
    }
}
