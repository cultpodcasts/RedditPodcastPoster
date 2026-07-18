namespace Discovery;

public class DiscoverOptions
{
    public required string SearchSince { get; set; }

    /// <summary>
    /// Required. <see cref="DiscoveryLookbackMode.Static"/>: fixed <see cref="SearchSince"/> window
    /// (may intentionally overlap the schedule, e.g. 6h10m over 6h).
    /// <see cref="DiscoveryLookbackMode.Dynamic"/>: <c>since = lastSuccess - DynamicLookbackOverlap</c>
    /// with no <see cref="SearchSince"/> floor; falls back to the static window when no prior signal exists.
    /// Config key: <c>discover__LookbackMode</c>. No default — must be set explicitly.
    /// </summary>
    public DiscoveryLookbackMode? LookbackMode { get; set; }

    /// <summary>
    /// Optional overlap subtracted from the latest successful run when <see cref="LookbackMode"/> is Dynamic.
    /// <c>00:00:00</c> (default) means no overlap: <c>since = lastSuccess</c>.
    /// Config key: <c>discover__DynamicLookbackOverlap</c>.
    /// </summary>
    public TimeSpan? DynamicLookbackOverlap { get; set; }

    public bool ExcludeSpotify { get; set; }
    public bool IncludeYouTube { get; set; }
    public bool IncludeListenNotes { get; set; }
    public bool EnrichFromSpotify { get; set; }
    public bool EnrichFromApple { get; set; }
    public bool IncludeTaddy { get; set; }
    public TimeSpan? TaddyOffset { get; set; }

    public override string ToString()
    {
        var reportDefinition = new[]
        {
            new {displayName = "since", value = SearchSince},
            new {displayName = "lookback-mode", value = LookbackMode?.ToString() ?? "Unset"},
            new {displayName = "dynamic-lookback-overlap", value = DynamicLookbackOverlap?.ToString() ?? "Null"},
            new {displayName = "taddyOffset", value = TaddyOffset?.ToString() ?? "Null"},
            new {displayName = "exclude-spotify", value = ExcludeSpotify.ToString()},
            new {displayName = "include-you-tube", value = IncludeYouTube.ToString()},
            new {displayName = "include-listen-notes", value = IncludeListenNotes.ToString()},
            new {displayName = "include-taddy", value = IncludeTaddy.ToString()},
            new {displayName = "enrich-from-spotify", value = EnrichFromSpotify.ToString()},
            new {displayName = "enrich-from-apple", value = EnrichFromApple.ToString()}
        };

        return
            $"{nameof(DiscoverOptions)}: {string.Join(", ", reportDefinition.Select(y => $"{y.displayName}= '{y.value}'"))}.";
    }
}
