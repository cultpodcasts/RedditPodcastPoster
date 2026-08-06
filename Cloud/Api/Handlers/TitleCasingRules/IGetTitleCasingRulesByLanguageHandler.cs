using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.TitleCasingRules;

public interface IGetTitleCasingRulesByLanguageHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string language,
        CancellationToken c);
}
