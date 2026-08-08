using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.TitleCasingRules;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Handlers.TitleCasingRules;

public class DeleteTitleCasingRulesLowerCaseTermHandler(
    ITitleCasingRulesUpdateService titleCasingRulesUpdateService,
    ILogger<DeleteTitleCasingRulesLowerCaseTermHandler> logger) : IDeleteTitleCasingRulesLowerCaseTermHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TitleCasingRulesLanguageTerm body,
        CancellationToken c)
    {
        var result = await titleCasingRulesUpdateService.DeleteLowerCaseTermAsync(
            body.Language,
            body.Term,
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
        logger.LogError("Title casing rules lower-case term delete failed with unexpected status.");
        return ctx.InternalError();
    }
}
