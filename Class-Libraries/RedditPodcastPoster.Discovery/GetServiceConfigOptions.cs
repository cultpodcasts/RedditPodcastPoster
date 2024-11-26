namespace RedditPodcastPoster.Discovery;

public record GetServiceConfigOptions(
    DateTime Since,
    bool ExcludeSpotify,
    bool IncludeYouTube,
    bool IncludeListenNotes,
    bool IncludeTaddy,
    bool EnrichFromSpotify,
    bool EnrichFromApple,
    TimeSpan? TaddyOffset)
{
    public override string ToString()
    {
        return
            $"{nameof(Since)}='{Since:g}', {nameof(ExcludeSpotify)}= {ExcludeSpotify}, {nameof(IncludeYouTube)}= {IncludeYouTube}, {nameof(IncludeListenNotes)}= {IncludeListenNotes}, {nameof(IncludeTaddy)}= {IncludeTaddy}, {nameof(EnrichFromSpotify)}= {EnrichFromSpotify}, {nameof(EnrichFromApple)}= {EnrichFromApple}, {nameof(TaddyOffset)}= '{TaddyOffset.ToString() ?? "null"}'.";
    }
}