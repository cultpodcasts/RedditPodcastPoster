namespace Discovery;

public static class DiscoveryLookbackCalculator
{
    /// <summary>
    /// Default Dynamic overlap (10 minutes), mirroring the production static 6h10m vs 6h schedule overlap.
    /// Config <c>discover__DynamicLookbackOverlap</c> of <c>00:00:00</c> means no overlap:
    /// <c>since = lastSuccess</c>.
    /// </summary>
    public static readonly TimeSpan DefaultDynamicOverlap = TimeSpan.FromMinutes(10);

    public static DateTime ResolveSince(
        DateTime utcNow,
        TimeSpan searchSince,
        DiscoveryLookbackMode mode,
        DateTime? latestSuccessfulDiscoveryBegan,
        TimeSpan? dynamicOverlap = null)
    {
        if (mode != DiscoveryLookbackMode.Dynamic || latestSuccessfulDiscoveryBegan is null)
        {
            return utcNow.Subtract(searchSince);
        }

        var overlap = dynamicOverlap ?? DefaultDynamicOverlap;
        // Anchor to last success (minus optional overlap). Do not floor at SearchSince —
        // that forced a full static window even when the prior run was minutes ago.
        return latestSuccessfulDiscoveryBegan.Value.ToUniversalTime().Subtract(overlap);
    }
}
