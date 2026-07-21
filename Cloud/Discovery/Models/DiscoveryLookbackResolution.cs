namespace Discovery.Models;

public sealed record DiscoveryLookbackResolution(
    DateTime Since,
    DateTime LatestSuccessfulDiscoveryBegan);
