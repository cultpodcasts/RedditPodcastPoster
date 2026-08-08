using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.SupportedLanguages;

public interface IPostSupportedLanguagesHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SupportedLanguageAddRequest body,
        CancellationToken c);
}
