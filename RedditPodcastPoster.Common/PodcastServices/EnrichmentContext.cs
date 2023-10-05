using Microsoft.Azure.Cosmos.Core.Collections;

namespace RedditPodcastPoster.Common.PodcastServices;

public record EnrichmentContext
{
    private Uri? _apple;
    private Uri? _spotify;
    private Uri? _youTube;
    private DateTime _release;

    public bool Updated => YouTubeUrlUpdated || SpotifyUrlUpdated || AppleUrlUpdated || AppleReleaseUpdated;

    public bool YouTubeUrlUpdated { get; private set; }
    public bool SpotifyUrlUpdated { get; private set; }
    public bool AppleUrlUpdated { get; private set; }
    public bool AppleReleaseUpdated { get; private set; }

    public Uri? YouTube
    {
        get => _youTube;
        set
        {
            _youTube = value;
            YouTubeUrlUpdated = true;
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
            AppleReleaseUpdated= true;
        }
    }
}