namespace Discovery;

public static class DiscoveryLookbackCalculator
{
    public static readonly TimeSpan DefaultDynamicOverlap = TimeSpan.FromMinutes(10);

    public static DateTime ResolveSince(
        DateTime utcNow,
        TimeSpan searchSince,
        DiscoveryLookbackMode mode,
        DateTime? latestSuccessfulDiscoveryBegan,
        TimeSpan? dynamicOverlap = null)
    {
        var staticSince = utcNow.Subtract(searchSince);
        if (mode != DiscoveryLookbackMode.Dynamic || latestSuccessfulDiscoveryBegan is null)
        {
            return staticSince;
        }

        var overlap = dynamicOverlap ?? DefaultDynamicOverlap;
        var dynamicSince = latestSuccessfulDiscoveryBegan.Value.ToUniversalTime().Subtract(overlap);

        // Prefer the earlier bound so missed runs extend lookback; when the last run is recent,
        // keep at least the configured static window (including its intentional overlap).
        return dynamicSince < staticSince ? dynamicSince : staticSince;
    }
}
