namespace Discovery;

public class DiscoverOptions
{
    public required string SearchSince { get; set; }

    /// <summary>
    /// Static (default): fixed <see cref="SearchSince"/> window with intentional schedule overlap.
    /// Dynamic: anchor to latest successful Discovery run signal; fall back to static when none exists.
    /// </summary>
    public DiscoveryLookbackMode LookbackMode { get; set; } = DiscoveryLookbackMode.Static;

    /// <summary>
    /// Overlap subtracted from the latest successful run when <see cref="LookbackMode"/> is Dynamic.
    /// Defaults to 10 minutes to mirror the production static 6h10m vs 6h schedule overlap.
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
            new {displayName = "lookback-mode", value = LookbackMode.ToString()},
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
