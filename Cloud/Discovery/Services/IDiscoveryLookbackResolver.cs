using Discovery.Models;

namespace Discovery.Services;

public interface IDiscoveryLookbackResolver
{
    Task<DiscoveryLookbackResolution> ResolveAsync(CancellationToken cancellationToken = default);
}
