using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.SupportedLanguages;

public interface IGetSupportedLanguagesHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c);
}
