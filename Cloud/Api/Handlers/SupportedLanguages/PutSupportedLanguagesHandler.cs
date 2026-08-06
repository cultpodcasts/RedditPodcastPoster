using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.SupportedLanguages;

namespace Api.Handlers.SupportedLanguages;

public class PutSupportedLanguagesHandler(
    ISupportedLanguagesUpdateService supportedLanguagesUpdateService,
    ILogger<PutSupportedLanguagesHandler> logger) : IPutSupportedLanguagesHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SupportedLanguagesUpdateRequest body,
        CancellationToken c)
    {
        var result = await supportedLanguagesUpdateService.UpdateAsync(body, c);
        return result.Status switch
        {
            SupportedLanguagesUpdateStatus.Ok =>
                await ctx.Ok(
                    SupportedLanguagesResponseBuilder.Build(result.Config!, isDefault: false),
                    c),
            SupportedLanguagesUpdateStatus.BadRequest =>
                await ctx.BadRequest(new { error = result.Error }, c),
            SupportedLanguagesUpdateStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Supported languages update failed with unexpected status.");
        return ctx.InternalError();
    }
}
