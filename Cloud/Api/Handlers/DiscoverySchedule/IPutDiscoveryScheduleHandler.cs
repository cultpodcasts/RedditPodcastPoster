using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Models;

namespace Api.Handlers.DiscoverySchedule;

public interface IPutDiscoveryScheduleHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        DiscoveryScheduleUpdateRequest body,
        CancellationToken c);
}
