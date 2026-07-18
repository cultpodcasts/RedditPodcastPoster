namespace Discovery;

public interface IDiscoveryLookbackResolver
{
    Task<DiscoveryLookbackResolution> ResolveAsync(CancellationToken cancellationToken = default);
}

public sealed record DiscoveryLookbackResolution(
    DateTime Since,
    DiscoveryLookbackMode ModeUsed,
    DateTime? LatestSuccessfulDiscoveryBegan);
