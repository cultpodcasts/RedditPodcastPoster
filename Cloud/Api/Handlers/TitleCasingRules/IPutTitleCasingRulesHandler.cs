using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.TitleCasingRules;

public interface IPutTitleCasingRulesHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TitleCasingRulesLanguageUpdate body,
        CancellationToken c);
}
