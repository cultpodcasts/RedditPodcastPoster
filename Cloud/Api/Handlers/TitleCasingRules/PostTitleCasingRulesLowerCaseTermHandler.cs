using Api.Dtos;
using Api.Models;
using Api.Services.TitleCasingRules;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Handlers.TitleCasingRules;

public class PostTitleCasingRulesLowerCaseTermHandler(
    ITitleCasingRulesUpdateService titleCasingRulesUpdateService,
    ILogger<PostTitleCasingRulesLowerCaseTermHandler> logger) : IPostTitleCasingRulesLowerCaseTermHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TitleCasingRulesLowerCaseTermAdd body,
        CancellationToken c)
    {
        var result = await titleCasingRulesUpdateService.AddLowerCaseTermAsync(
            body.Language,
            body.Term,
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
        logger.LogError("Title casing rules lower-case term add failed with unexpected status.");
        return ctx.InternalError();
    }
}
