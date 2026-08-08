using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.TitleCasingRules;

public interface IDeleteTitleCasingRulesKnownTermHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TitleCasingRulesLanguageKnownTermDelete body,
        CancellationToken c);
}
