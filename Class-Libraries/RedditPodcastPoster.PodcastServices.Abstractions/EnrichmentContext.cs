namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record EnrichmentContext
{
    public bool Updated => YouTubeUrlUpdated || 
                           SpotifyUrlUpdated || 
                           AppleUrlUpdated || 
                           AppleReleaseUpdated ||
                           YouTubeIdUpdated;

    public bool YouTubeUrlUpdated { get; private set; }
    public bool SpotifyUrlUpdated { get; private set; }
    public bool AppleUrlUpdated { get; private set; }
    public bool AppleReleaseUpdated { get; private set; }
    public bool YouTubeIdUpdated { get; private set; }

    public Uri? YouTube
    {
        set => YouTubeUrlUpdated = true;
    }

    public string? YouTubeId
    {
        set => YouTubeIdUpdated = true;
    }

    public Uri? Spotify
    {
        set => SpotifyUrlUpdated = true;
    }

    public Uri? Apple
    {
        set => AppleUrlUpdated = true;
    }

    public DateTime Release
    {
        set => AppleReleaseUpdated = true;
    }
}