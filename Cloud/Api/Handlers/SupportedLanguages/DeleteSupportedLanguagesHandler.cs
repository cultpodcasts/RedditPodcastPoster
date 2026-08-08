using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.SupportedLanguages;

namespace Api.Handlers.SupportedLanguages;

public class DeleteSupportedLanguagesHandler(
    ISupportedLanguagesUpdateService supportedLanguagesUpdateService,
    ILogger<DeleteSupportedLanguagesHandler> logger) : IDeleteSupportedLanguagesHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string code,
        CancellationToken c)
    {
        var result = await supportedLanguagesUpdateService.DeleteAsync(code, c);
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
        logger.LogError("Supported languages delete failed with unexpected status.");
        return ctx.InternalError();
    }
}
