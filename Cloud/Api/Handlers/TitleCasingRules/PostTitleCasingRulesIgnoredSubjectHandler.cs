using Api.Dtos;
using Api.Models;
using Api.Services.TitleCasingRules;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Handlers.TitleCasingRules;

public class PostTitleCasingRulesIgnoredSubjectHandler(
    ITitleCasingRulesUpdateService titleCasingRulesUpdateService,
    ILogger<PostTitleCasingRulesIgnoredSubjectHandler> logger) : IPostTitleCasingRulesIgnoredSubjectHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TitleCasingRulesLanguageTerm body,
        CancellationToken c)
    {
        var result = await titleCasingRulesUpdateService.AddIgnoredSubjectAsync(
            body.Language,
            new TitleCasingRulesAddLowerCaseTermRequest { Term = body.Term },
            c);
        return result.Status switch
        {
            TitleCasingRulesUpdateStatus.Ok =>
                ctx.Accepted(),
            TitleCasingRulesUpdateStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Bad request"), c),
            TitleCasingRulesUpdateStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Title casing rules ignored-subject add failed with unexpected status.");
        return ctx.InternalError();
    }
}
