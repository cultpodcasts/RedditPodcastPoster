using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.Episodes;

/// <summary>
/// Test-only catalog adapters so existing arrange/assert that still names leftover DTO
/// members compile against nested <c>ids</c> / <c>services</c>. Production assemblies do not
/// reference this project.
/// </summary>
public static class EpisodeCatalogTestExtensions
{
    extension(Episode episode)
    {
        public string SpotifyId
        {
            get => EpisodeServicePresence.SpotifyEpisodeId(episode) ?? string.Empty;
            set => EpisodeServicePresence.SetSpotifyIdentity(episode, value);
        }

        public long? AppleId
        {
            get => EpisodeServicePresence.AppleEpisodeId(episode);
            set => EpisodeServicePresence.SetAppleIdentity(episode, value);
        }

        public string YouTubeId
        {
            get => EpisodeServicePresence.YouTubeEpisodeId(episode) ?? string.Empty;
            set => EpisodeServicePresence.SetYouTubeIdentity(episode, value);
        }

        public EpisodeCatalogUrls Urls
        {
            get => EpisodeCatalogUrls.For(episode);
            set => EpisodeCatalogUrls.Replace(episode, value);
        }

        public EpisodeCatalogImages? Images
        {
            get => EpisodeServicePresence.ToEpisodeImages(episode) is null
                ? null
                : EpisodeCatalogImages.For(episode);
            set => EpisodeCatalogImages.Replace(episode, value);
        }
    }

    public static void ApplyListenUrl(this Episode episode, string key, Uri? url)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var image = EpisodeServicePresence.TryGetImage(episode, key);
        EpisodeServicePresence.Upsert(episode, key, url, image);
        if (url is not null || episode.Services is null ||
            !episode.Services.TryGetValue(key, out var link))
        {
            return;
        }

        link.Url = null;
        if (link.Image is null)
        {
            EpisodeServicePresence.Upsert(episode, key, null, null);
        }
    }
}

/// <summary>
/// Write-through view of catalog listen URLs. Assignment of <see cref="ServiceUrls"/>
/// replaces the leftover-shaped slots; property setters mutate a single catalog key.
/// </summary>
public sealed class EpisodeCatalogUrls
{
    private readonly Episode? _live;
    private Uri? _spotify;
    private Uri? _apple;
    private Uri? _youTube;
    private Uri? _internetArchive;
    private Uri? _bbc;

    private EpisodeCatalogUrls(Episode live)
    {
        _live = live;
    }

    private EpisodeCatalogUrls(ServiceUrls snapshot)
    {
        _spotify = snapshot.Spotify;
        _apple = snapshot.Apple;
        _youTube = snapshot.YouTube;
        _internetArchive = snapshot.InternetArchive;
        _bbc = snapshot.BBC;
    }

    public static EpisodeCatalogUrls For(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return new EpisodeCatalogUrls(episode);
    }

    public static implicit operator EpisodeCatalogUrls(ServiceUrls urls)
    {
        ArgumentNullException.ThrowIfNull(urls);
        return new EpisodeCatalogUrls(urls);
    }

    public Uri? Spotify
    {
        get => Read(ServiceKeys.Spotify, _spotify);
        set => Write(ServiceKeys.Spotify, value, ref _spotify);
    }

    public Uri? Apple
    {
        get => Read(ServiceKeys.Apple, _apple);
        set => Write(ServiceKeys.Apple, value, ref _apple);
    }

    public Uri? YouTube
    {
        get => Read(ServiceKeys.YouTube, _youTube);
        set => Write(ServiceKeys.YouTube, value, ref _youTube);
    }

    public Uri? InternetArchive
    {
        get => Read(ServiceKeys.InternetArchive, _internetArchive);
        set => Write(ServiceKeys.InternetArchive, value, ref _internetArchive);
    }

    public Uri? BBC
    {
        get => _live is not null
            ? EpisodeServicePresence.TryGetUrl(_live, ServiceKeys.BbcIplayer) ??
              EpisodeServicePresence.TryGetUrl(_live, ServiceKeys.BbcSounds)
            : _bbc;
        set
        {
            if (_live is not null)
            {
                ApplyBbc(_live, value);
                return;
            }

            _bbc = value;
        }
    }

    public static void Replace(Episode episode, EpisodeCatalogUrls? urls)
    {
        ArgumentNullException.ThrowIfNull(episode);
        episode.ApplyListenUrl(ServiceKeys.Spotify, urls?.Spotify);
        episode.ApplyListenUrl(ServiceKeys.Apple, urls?.Apple);
        episode.ApplyListenUrl(ServiceKeys.YouTube, urls?.YouTube);
        episode.ApplyListenUrl(ServiceKeys.InternetArchive, urls?.InternetArchive);
        ApplyBbc(episode, urls?.BBC);
    }

    private Uri? Read(string key, Uri? snapshot) =>
        _live is not null ? EpisodeServicePresence.TryGetUrl(_live, key) : snapshot;

    private void Write(string key, Uri? value, ref Uri? snapshot)
    {
        if (_live is not null)
        {
            _live.ApplyListenUrl(key, value);
            return;
        }

        snapshot = value;
    }

    private static void ApplyBbc(Episode episode, Uri? url)
    {
        episode.ApplyListenUrl(ServiceKeys.BbcIplayer, null);
        episode.ApplyListenUrl(ServiceKeys.BbcSounds, null);
        if (url is null)
        {
            return;
        }

        var key = ServiceCatalog.TryResolveKey(url) ?? ServiceKeys.BbcSounds;
        episode.ApplyListenUrl(key, url);
    }
}

/// <summary>
/// Write-through view of catalog artwork. Assignment of <see cref="EpisodeImages"/>
/// replaces leftover-shaped image slots.
/// </summary>
public sealed class EpisodeCatalogImages
{
    private readonly Episode? _live;
    private Uri? _youTube;
    private Uri? _spotify;
    private Uri? _apple;
    private Uri? _other;

    private EpisodeCatalogImages(Episode live)
    {
        _live = live;
    }

    private EpisodeCatalogImages(EpisodeImages snapshot)
    {
        _youTube = snapshot.YouTube;
        _spotify = snapshot.Spotify;
        _apple = snapshot.Apple;
        _other = snapshot.Other;
    }

    public static EpisodeCatalogImages For(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return new EpisodeCatalogImages(episode);
    }

    public static implicit operator EpisodeCatalogImages(EpisodeImages images)
    {
        ArgumentNullException.ThrowIfNull(images);
        return new EpisodeCatalogImages(images);
    }

    public Uri? YouTube
    {
        get => Read(ServiceKeys.YouTube, _youTube);
        set => Write(ServiceKeys.YouTube, value, ref _youTube);
    }

    public Uri? Spotify
    {
        get => Read(ServiceKeys.Spotify, _spotify);
        set => Write(ServiceKeys.Spotify, value, ref _spotify);
    }

    public Uri? Apple
    {
        get => Read(ServiceKeys.Apple, _apple);
        set => Write(ServiceKeys.Apple, value, ref _apple);
    }

    public Uri? Other
    {
        get => _live is not null
            ? EpisodeServicePresence.ToEpisodeImages(_live)?.Other
            : _other;
        set
        {
            if (_live is not null)
            {
                ApplyOther(_live, value);
                return;
            }

            _other = value;
        }
    }

    public static void Replace(Episode episode, EpisodeCatalogImages? images)
    {
        ArgumentNullException.ThrowIfNull(episode);
        EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.YouTube, images?.YouTube);
        EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.Spotify, images?.Spotify);
        EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.Apple, images?.Apple);
        ApplyOther(episode, images?.Other);
    }

    private Uri? Read(string key, Uri? snapshot) =>
        _live is not null ? EpisodeServicePresence.TryGetImage(_live, key) : snapshot;

    private void Write(string key, Uri? value, ref Uri? snapshot)
    {
        if (_live is not null)
        {
            EpisodeServicePresence.SetCatalogImage(_live, key, value);
            return;
        }

        snapshot = value;
    }

    private static void ApplyOther(Episode episode, Uri? image)
    {
        foreach (var key in ServiceCatalog.ImageCoalesceOrder)
        {
            if (key is ServiceKeys.YouTube or ServiceKeys.Spotify or ServiceKeys.Apple)
            {
                continue;
            }

            if (EpisodeServicePresence.HasUrl(episode, key))
            {
                EpisodeServicePresence.SetCatalogImage(episode, key, image);
                return;
            }
        }

        if (image is not null)
        {
            EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.BbcSounds, image);
        }
    }
}
