namespace Discovery;

public static class DiscoveryLookbackCalculator
{
    /// <summary>
    /// Default Dynamic overlap: none. Config <c>discover__DynamicLookbackOverlap</c> of
    /// <c>00:00:00</c> (or unset) means <c>since = lastSuccess</c> with no re-search of the prior window.
    /// </summary>
    public static readonly TimeSpan DefaultDynamicOverlap = TimeSpan.Zero;

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
