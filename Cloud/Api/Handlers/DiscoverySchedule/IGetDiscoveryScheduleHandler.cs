using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.DiscoverySchedule;

public interface IGetDiscoveryScheduleHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c);
}
