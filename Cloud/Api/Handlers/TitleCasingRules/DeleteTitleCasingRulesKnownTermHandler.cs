using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.TitleCasingRules;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Handlers.TitleCasingRules;

public class DeleteTitleCasingRulesKnownTermHandler(
    ITitleCasingRulesUpdateService titleCasingRulesUpdateService,
    ILogger<DeleteTitleCasingRulesKnownTermHandler> logger) : IDeleteTitleCasingRulesKnownTermHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TitleCasingRulesLanguageKnownTermDelete body,
        CancellationToken c)
    {
        var result = await titleCasingRulesUpdateService.DeleteKnownTermAsync(
            body.Language,
            body.Literal,
            c);
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
        logger.LogError("Title casing rules known term delete failed with unexpected status.");
        return ctx.InternalError();
    }
}
