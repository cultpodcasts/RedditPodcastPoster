using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.TitleCasingRules;

public interface IPostTitleCasingRulesKnownTermHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TitleCasingRulesLanguageKnownTermAdd body,
        CancellationToken c);
}
