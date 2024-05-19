using Api.Dtos;

namespace Api.Services;

public interface IDiscoveryResultsService
{
    Task<DiscoveryResults> Get(CancellationToken c);
    Task MarkAsProcessed(Guid[] documentIds);
}