namespace RedditPodcastPoster.Common.PodcastServices;

public record EnrichmentContext
{
    private Uri? _apple;
    private DateTime _release;
    private Uri? _spotify;
    private Uri? _youTube;
    private string? _youTubeId;

    public bool Updated => YouTubeUrlUpdated || SpotifyUrlUpdated || AppleUrlUpdated || AppleReleaseUpdated ||
                           YouTubeIdUpdated;

    public bool YouTubeUrlUpdated { get; private set; }
    public bool SpotifyUrlUpdated { get; private set; }
    public bool AppleUrlUpdated { get; private set; }
    public bool AppleReleaseUpdated { get; private set; }
    public bool YouTubeIdUpdated { get; private set; }

    public Uri? YouTube
    {
        get => _youTube;
        set
        {
            _youTube = value;
            YouTubeUrlUpdated = true;
        }
    }

    public string YouTubeId
    {
        get => _youTubeId;
        set
        {
            _youTubeId = value;
            YouTubeIdUpdated = true;
        }
    }

    public Uri? Spotify
    {
        get => _spotify;
        set
        {
            _spotify = value;
            SpotifyUrlUpdated = true;
        }
    }

    public Uri? Apple
    {
        get => _apple;
        set
        {
            _apple = value;
            AppleUrlUpdated = true;
        }
    }

    public DateTime Release
    {
        get => _release;
        set
        {
            _release = value;
            AppleReleaseUpdated = true;
        }
    }
}