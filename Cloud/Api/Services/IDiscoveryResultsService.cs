using Api.Dtos;

namespace Api.Services;

public interface IDiscoveryResultsService
{
    Task<DiscoveryResults> Get(CancellationToken c);
}