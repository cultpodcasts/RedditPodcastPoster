namespace RedditPodcastPoster.Common.PodcastServices;

public record EnrichmentContext
{
    private Uri? _apple;
    private Uri? _spotify;
    private Uri? _youTube;

    public bool Updated => YouTubeUpdated || SpotifyUpdated || AppleUpdated;

    public bool YouTubeUpdated { get; private set; }
    public bool SpotifyUpdated { get; private set; }
    public bool AppleUpdated { get; private set; }

    public Uri? YouTube
    {
        get => _youTube;
        set
        {
            _youTube = value;
            YouTubeUpdated = true;
        }
    }

    public Uri? Spotify
    {
        get => _spotify;
        set
        {
            _spotify = value;
            SpotifyUpdated = true;
        }
    }

    public Uri? Apple
    {
        get => _apple;
        set
        {
            _apple = value;
            AppleUpdated = true;
        }
    }
}