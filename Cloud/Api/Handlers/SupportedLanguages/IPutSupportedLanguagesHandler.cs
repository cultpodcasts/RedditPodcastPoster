using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.SupportedLanguages;

public interface IPutSupportedLanguagesHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SupportedLanguagesUpdateRequest body,
        CancellationToken c);
}
