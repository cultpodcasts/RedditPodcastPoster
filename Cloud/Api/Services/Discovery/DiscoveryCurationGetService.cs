using Api.Models;
using Microsoft.Extensions.Logging;

namespace Api.Services.Discovery;

public class DiscoveryCurationGetService(
    IDiscoveryResultsService discoveryResultsService,
    ILogger<DiscoveryCurationGetService> logger) : IDiscoveryCurationGetService
{
    public async Task<DiscoveryCurationGetResult> GetAsync(bool includeHidden, CancellationToken cancellationToken)
    {
        try
        {
            var result = await discoveryResultsService.Get(includeHidden, cancellationToken);
            return new DiscoveryCurationGetResult(DiscoveryCurationGetStatus.Ok, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to obtain discovery-results.");
            return new DiscoveryCurationGetResult(DiscoveryCurationGetStatus.Failed);
        }
    }
}
