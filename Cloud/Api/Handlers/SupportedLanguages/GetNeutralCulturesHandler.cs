using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;

namespace Api.Handlers.SupportedLanguages;

public interface IGetNeutralCulturesHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c);
}

public class GetNeutralCulturesHandler(
    ILogger<GetNeutralCulturesHandler> logger) : IGetNeutralCulturesHandler
{
    public Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c)
    {
        try
        {
            return ctx.Ok(NeutralCulturesResponse.FromLookup(), c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to list neutral cultures.");
            return Task.FromResult(ctx.InternalError());
        }
    }
}
