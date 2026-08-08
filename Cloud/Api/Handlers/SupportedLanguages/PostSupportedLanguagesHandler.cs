using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.SupportedLanguages;

namespace Api.Handlers.SupportedLanguages;

public class PostSupportedLanguagesHandler(
    ISupportedLanguagesUpdateService supportedLanguagesUpdateService,
    ILogger<PostSupportedLanguagesHandler> logger) : IPostSupportedLanguagesHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SupportedLanguageAddRequest body,
        CancellationToken c)
    {
        var result = await supportedLanguagesUpdateService.AddAsync(body, c);
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
        logger.LogError("Supported languages add failed with unexpected status.");
        return ctx.InternalError();
    }
}
