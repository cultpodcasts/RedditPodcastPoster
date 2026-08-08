using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.SupportedLanguages;

public interface IDeleteSupportedLanguagesHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string code,
        CancellationToken c);
}
