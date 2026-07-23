using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Homepage;

public interface IPublishHomepageHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c);
}
