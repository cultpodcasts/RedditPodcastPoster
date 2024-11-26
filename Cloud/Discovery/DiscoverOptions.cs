namespace Discovery;

public class DiscoverOptions
{
    public required string SearchSince { get; set; }
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