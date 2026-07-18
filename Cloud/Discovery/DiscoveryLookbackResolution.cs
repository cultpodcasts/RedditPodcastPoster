namespace Discovery;

public sealed record DiscoveryLookbackResolution(
    DateTime Since,
    DiscoveryLookbackMode ModeUsed,
    DateTime? LatestSuccessfulDiscoveryBegan);
