namespace Discovery;

public sealed record DiscoveryLookbackResolution(
    DateTime Since,
    DateTime LatestSuccessfulDiscoveryBegan);
