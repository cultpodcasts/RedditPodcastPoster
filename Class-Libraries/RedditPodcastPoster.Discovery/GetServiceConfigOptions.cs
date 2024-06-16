namespace RedditPodcastPoster.Discovery;

public record GetServiceConfigOptions(
    bool ExcludeSpotify,
    bool IncludeYouTube,
    bool IncludeListenNotes,
    bool IncludeTaddy)
{
    public override string ToString()
    {
        return
            $"{nameof(ExcludeSpotify)}= {ExcludeSpotify}, {nameof(IncludeYouTube)}= {IncludeYouTube}, {nameof(IncludeListenNotes)}= {IncludeListenNotes}, {nameof(IncludeTaddy)}= {IncludeTaddy}.";
    }
}