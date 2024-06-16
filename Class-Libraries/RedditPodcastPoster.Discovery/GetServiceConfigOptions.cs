namespace RedditPodcastPoster.Discovery;

public record GetServiceConfigOptions(bool ExcludeSpotify, bool IncludeYouTube, bool IncludeListenNotes)
{
    public override string ToString()
    {
        return $"{nameof(ExcludeSpotify)}= {ExcludeSpotify}, {nameof(IncludeYouTube)}= {IncludeYouTube}, {nameof(IncludeListenNotes)}= {IncludeListenNotes}.";
    }
}