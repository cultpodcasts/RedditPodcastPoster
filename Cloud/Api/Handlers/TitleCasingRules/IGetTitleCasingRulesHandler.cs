using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.TitleCasingRules;

public interface IGetTitleCasingRulesHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c);
}
