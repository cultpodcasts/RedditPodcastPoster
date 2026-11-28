namespace Azure;

/// <summary>
/// TTL policy for Cosmos Activity booking docs (#1302). The ledger stays (cross-instance
/// initiate/complete lock); completed and orphan initiate docs must not grow forever.
/// Values are applied by the <c>bookActivity</c> stored procedure — keep
/// <c>Azure-Resources/CosmosDb/bookActivity.js</c> and the bicep copy in sync.
/// </summary>
public static class ActivityBookingTtl
{
    /// <summary>
    /// Orphan <c>initiate</c> rows (crash before complete) expire after two days.
    /// Hourly/discovery runs finish far sooner; Durable retries of the same operation id
    /// still see the lock within this window.
    /// </summary>
    public const int InitiateSeconds = 2 * 24 * 60 * 60;

    /// <summary>
    /// <c>complete</c> rows keep the "Already Complete" guard for Durable replays, then
    /// auto-purge after a week so the container does not accumulate forever.
    /// </summary>
    public const int CompleteSeconds = 7 * 24 * 60 * 60;
}
