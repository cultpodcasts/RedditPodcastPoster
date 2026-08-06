using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.SupportedLanguages;

namespace Api.Handlers.SupportedLanguages;

public class GetSupportedLanguagesHandler(
    ISupportedLanguagesGetService supportedLanguagesGetService,
    ILogger<GetSupportedLanguagesHandler> logger) : IGetSupportedLanguagesHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c)
    {
        var result = await supportedLanguagesGetService.GetAsync(c);
        return result.Status switch
        {
            SupportedLanguagesGetStatus.Ok =>
                await ctx.Ok(
                    SupportedLanguagesResponseBuilder.Build(result.Config!, result.IsDefault),
                    c),
            SupportedLanguagesGetStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Supported languages get failed with unexpected status.");
        return ctx.InternalError();
    }
}
