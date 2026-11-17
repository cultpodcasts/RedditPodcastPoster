namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <summary>
/// The D1 search sync (epic #1312, SP-2.1 #1321), configuration section <c>d1Search</c>: the <b>production</b> D1
/// search index, reached through the api Worker's <c>POST /search/admin/docs</c> with an Auth0 M2M token for the prod
/// tenant's <c>search-index-writer</c> app. There are only production Functions, so there is one index and no preview
/// target (Jon, 17 Nov 2026). Credentials reach the app settings from Key Vault (<c>Search-Index-Writer-ClientId</c> / <c>-ClientSecret</c>, SP-2.5 #1460).
/// </summary>
public sealed class D1SearchOptions
{
    /// <summary>The switch (<c>d1SearchProductionEnabled</c> in bicep, off until Jon's OK): when off nothing is sent.</summary>
    public bool Enabled { get; set; }

    /// <summary>api Worker base URL (the Bot Fight bypass host, <c>api__Endpoint</c>); routes are relative to it.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>Auth0 tenant (custom) domain the token is requested from, e.g. <c>auth.cultpodcasts.com</c>.</summary>
    public string Auth0Domain { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Enabled and every value present. Enabled without credentials is skipped and logged.</summary>
    public bool IsUsable => Enabled && Endpoint is not null && !string.IsNullOrWhiteSpace(Auth0Domain) &&
                            !string.IsNullOrWhiteSpace(Audience) && !string.IsNullOrWhiteSpace(ClientId) &&
                            !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>
    /// How far back the hourly drip (<see cref="D1SearchChangeFeed"/>) looks for changed playables, by Cosmos
    /// <c>_ts</c>. Larger than the hour between runs so a late or failed run is covered by the next one; replays
    /// are harmless (the Worker's <c>modifiedAt</c> guard makes an equal upsert a no-op retry).
    /// </summary>
    public int DripLookbackMinutes { get; set; } = 180;

    /// <summary>Delay before the single retry of a retryable write failure (429, 5xx, network).</summary>
    public int RetryDelayMilliseconds { get; set; } = 2000;

    /// <summary>The weekly reconcile (SP-2.2 #1322), section <c>d1Search:Reconcile</c>.</summary>
    public D1SearchReconcileOptions Reconcile { get; set; } = new();
}

/// <summary>
/// The weekly Cosmos-to-D1 reconcile (SP-2.2 #1322). <b>Dry-run by default:</b> the timer patches the index only when
/// <see cref="Write"/> is true (false by default, Jon's call), and never when the drift is above
/// <see cref="DriftThreshold"/>. The initial backfill runs from the <c>D1SearchReconcile</c> console with
/// <c>--write --allow-drift</c>.
/// </summary>
public sealed class D1SearchReconcileOptions
{
    /// <summary>The timer may patch the index. When false (the default) it reports only.</summary>
    public bool Write { get; set; }

    /// <summary>
    /// The share of the index (missing + stale + orphans, over the larger of D1 and Cosmos) above which the reconcile
    /// refuses to patch: that much drift means something is broken, and the fix is a full rebuild (SP-2.3) or a
    /// deliberate backfill (the console's <c>--allow-drift</c>), not a quiet weekly patch.
    /// </summary>
    public double DriftThreshold { get; set; } = 0.05;
}
