namespace Discovery;

public static class DiscoveryLookbackCalculator
{
    /// <summary>
    /// Default Dynamic overlap (10 minutes).
    /// Config <c>discover__DynamicLookbackOverlap</c> of <c>00:00:00</c> means no overlap:
    /// <c>since = lastSuccess</c>.
    /// </summary>
    public static readonly TimeSpan DefaultDynamicOverlap = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Dynamic-only: <c>since = lastSuccess - overlap</c>.
    /// Caller must ensure <paramref name="latestSuccessfulDiscoveryBegan"/> is non-null
    /// (fail closed otherwise).
    /// </summary>
    public static DateTime ResolveSince(
        DateTime latestSuccessfulDiscoveryBegan,
        TimeSpan? dynamicOverlap = null)
    {
        var overlap = dynamicOverlap ?? DefaultDynamicOverlap;
        return latestSuccessfulDiscoveryBegan.ToUniversalTime().Subtract(overlap);
    }
}
