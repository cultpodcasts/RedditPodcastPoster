﻿namespace Discovery;

public class DiscoverOptions
{
    public required string SearchSince { get; set; }
    public bool ExcludeSpotify { get; set; }
    public bool IncludeYouTube { get; set; }
    public bool IncludeListenNotes { get; set; }
    public bool EnrichListenNotesFromSpotify { get; set; }
    public bool EnrichSpotifyFromApple { get; set; }
    public bool IncludeTaddy { get; set; }

    public override string ToString()
    {
        var reportDefinition = new[]
        {
            new {displayName = "since", value = SearchSince},
            new {displayName = "exclude-spotify", value = ExcludeSpotify.ToString()},
            new {displayName = "include-you-tube", value = IncludeYouTube.ToString()},
            new {displayName = "include-listen-notes", value = IncludeListenNotes.ToString()},
            new {displayName = "include-taddy", value = IncludeTaddy.ToString()},
            new {displayName = "enrich-listen-notes-from-spotify", value = EnrichListenNotesFromSpotify.ToString()},
            new {displayName = "enrich-spotify-from-apple", value = EnrichSpotifyFromApple.ToString()}
        };

        return
            $"{nameof(DiscoverOptions)}: {string.Join(", ", reportDefinition.Select(y => $"{y.displayName}= '{y.value}'"))}.";
    }
}