namespace Discovery;

public class DiscoverOptions
{
    public required string SearchSince { get; set; }
    public bool ExcludeSpotify { get; set; }
    public bool IncludeYouTube { get; set; }
    public bool IncludeListenNotes { get; set; }
    public bool EnrichListenNotesFromSpotify { get; set; }

    public override string ToString()
    {
        return $"{nameof(DiscoverOptions)}: since: '{SearchSince}', exclude-spotify: '{ExcludeSpotify}', include-you-tube: '{IncludeYouTube}', include-listen-notes: '{IncludeListenNotes}'.";
    }
}