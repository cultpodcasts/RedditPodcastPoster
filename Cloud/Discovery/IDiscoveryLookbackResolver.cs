namespace Discovery;

public interface IDiscoveryLookbackResolver
{
    Task<DiscoveryLookbackResolution> ResolveAsync(CancellationToken cancellationToken = default);
}
