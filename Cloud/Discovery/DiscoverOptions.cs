namespace Discovery;

public class DiscoverOptions
{
    public string Since { get; set; }
    public bool ExcludeSpotify { get; set; }
    public bool IncludeYouTube { get; set; }
    public bool IncludeListenNotes { get; set; }

    public override string ToString()
    {
        return $"{nameof(DiscoverOptions)}: since: '{Since}', exclude-spotify: '{ExcludeSpotify}', include-you-tube: '{IncludeYouTube}', include-listen-notes: '{IncludeListenNotes}'.";
    }
}