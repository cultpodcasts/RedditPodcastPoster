using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.TitleCasingRules;

public interface IDeleteTitleCasingRulesLowerCaseTermHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TitleCasingRulesLanguageTerm body,
        CancellationToken c);
}
