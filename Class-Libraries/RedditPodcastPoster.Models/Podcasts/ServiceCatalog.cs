// pragma: allowlist secret
namespace RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret

/// <summary>
/// Well-known streaming/documentary services. JSON keys identify a service for icons;
/// <see cref="TryResolveKey"/> maps a URL host/path to the same key so unknown documents
/// and pasted links still get a logo.
/// </summary>
public static class ServiceCatalog
{
    public sealed record Descriptor(
        string Key,
        string DisplayName,
        string Icon,
        bool ReconstructableFromIndexIds,
        bool WideImage,
        IReadOnlyList<string> Hosts);

    public static readonly IReadOnlyList<Descriptor> All =
    [
        new(ServiceKeys.YouTube, "YouTube", "youtube", true, true, ["youtube.com", "m.youtube.com", "music.youtube.com", "youtu.be"]),
        new(ServiceKeys.Spotify, "Spotify", "spotify", true, false, ["open.spotify.com"]),
        new(ServiceKeys.Apple, "Apple Podcasts", "apple", true, false, ["podcasts.apple.com"]), // pragma: allowlist secret
        new(ServiceKeys.BbcIplayer, "BBC iPlayer", "bbc-iplayer", false, true, ["bbc.co.uk", "bbc.com"]),
        new(ServiceKeys.BbcSounds, "BBC Sounds", "bbc-sounds", false, false, ["bbc.co.uk", "bbc.com"]),
        new(ServiceKeys.InternetArchive, "Internet Archive", "internet-archive", false, true, ["archive.org"]),
        new(ServiceKeys.Vimeo, "Vimeo", "vimeo", false, true, ["vimeo.com"]),
        new(ServiceKeys.Netflix, "Netflix", "netflix", false, true, ["netflix.com"]),
        new(ServiceKeys.AmazonPrime, "Amazon Prime Video", "amazon-prime", false, true, ["primevideo.com", "amazon.com", "amazon.co.uk"]),
        new(ServiceKeys.Other, "Other", "external-service", false, false, [])
    ];

    private static readonly Dictionary<string, Descriptor> ByKey =
        All.ToDictionary(d => d.Key, StringComparer.Ordinal);

    /// <summary>
    /// Cover-art preference: YouTube frame, then Spotify, Apple, then remaining services.
    /// </summary>
    public static readonly string[] ImageCoalesceOrder =
    [
        ServiceKeys.YouTube,
        ServiceKeys.Spotify,
        ServiceKeys.Apple,
        ServiceKeys.BbcIplayer,
        ServiceKeys.BbcSounds,
        ServiceKeys.InternetArchive,
        ServiceKeys.Vimeo,
        ServiceKeys.Netflix,
        ServiceKeys.AmazonPrime,
        ServiceKeys.Other
    ];

    /// <summary>Editor default slots: Spotify, Apple, YouTube (same identity as <see cref="IndexIdKeys"/>, UI order).</summary>
    public static readonly string[] DefaultUiKeys =
    [
        ServiceKeys.Spotify,
        ServiceKeys.Apple,
        ServiceKeys.YouTube
    ];

    /// <summary>Services whose watch/listen URL is rebuilt from index id fields, not stored in <c>svc</c>.</summary>
    public static readonly string[] IndexIdKeys =
    [
        ServiceKeys.Spotify,
        ServiceKeys.YouTube,
        ServiceKeys.Apple
    ];

    /// <summary>Stable order for compacting non-index-id services into the search <c>svc</c> field.</summary>
    public static readonly string[] SearchEncodedKeys =
    [
        ServiceKeys.BbcSounds,
        ServiceKeys.BbcIplayer,
        ServiceKeys.InternetArchive,
        ServiceKeys.Vimeo,
        ServiceKeys.Netflix,
        ServiceKeys.AmazonPrime,
        ServiceKeys.Other
    ];

    public static bool TryGet(string key, out Descriptor descriptor) =>
        ByKey.TryGetValue(key, out descriptor!);

    public static bool IsIndexIdKey(string key) =>
        key is ServiceKeys.Spotify or ServiceKeys.YouTube or ServiceKeys.Apple;

    public static string? TryResolveKey(Uri url)
    {
        if (!url.IsAbsoluteUri)
        {
            return null;
        }

        var host = url.Host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        var path = url.AbsolutePath;

        if (IsHost(host, "youtu.be") || IsHost(host, "youtube.com") || IsHost(host, "m.youtube.com") ||
            IsHost(host, "music.youtube.com"))
        {
            return ServiceKeys.YouTube;
        }

        if (IsHost(host, "open.spotify.com"))
        {
            return ServiceKeys.Spotify;
        }

        if (IsHost(host, "podcasts.apple.com")) // pragma: allowlist secret
        {
            return ServiceKeys.Apple;
        }

        if (IsHost(host, "bbc.co.uk") || IsHost(host, "bbc.com"))
        {
            if (path.StartsWith("/sounds/", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceKeys.BbcSounds;
            }

            if (path.StartsWith("/iplayer/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/news/av-embeds/", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceKeys.BbcIplayer;
            }

            return ServiceKeys.BbcSounds;
        }

        if (IsHost(host, "archive.org"))
        {
            return ServiceKeys.InternetArchive;
        }

        if (IsHost(host, "vimeo.com"))
        {
            return ServiceKeys.Vimeo;
        }

        if (IsHost(host, "netflix.com"))
        {
            return ServiceKeys.Netflix;
        }

        if (IsHost(host, "primevideo.com") ||
            (IsAmazonHost(host) && IsAmazonVideoPath(path)))
        {
            return ServiceKeys.AmazonPrime;
        }

        return null;
    }

    /// <summary>
    /// Key for a URL that is not a well-known service: a host slug usable as a JSON key
    /// (letters/digits only, e.g. <c>dailymotioncom</c>).
    /// </summary>
    public static string KeyFromUnknownHost(Uri url)
    {
        var host = url.Host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        var chars = host.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? ServiceKeys.Other : new string(chars);
    }

    public static string ResolveOrHostKey(Uri url) =>
        TryResolveKey(url) ?? KeyFromUnknownHost(url);

    public static string? TryCompactUrl(string key, Uri url)
    {
        var text = url.ToString();
        return key switch
        {
            ServiceKeys.BbcSounds => TryTrimPrefixHostPath(text, ["/sounds/play/"], allowSlug: false),
            ServiceKeys.BbcIplayer => TryTrimPrefixHostPath(text, ["/iplayer/episode/"], allowSlug: true),
            ServiceKeys.InternetArchive => TryTrimPrefixHostPath(text, ["/details/"], allowSlug: false, hosts: ["archive.org"]),
            ServiceKeys.Vimeo => TryVimeoId(url),
            ServiceKeys.Netflix => TryTrimPrefixHostPath(text, ["/title/"], allowSlug: false, hosts: ["netflix.com"]),
            _ => null
        };
    }

    public static Uri? TryExpandCompactUrl(string key, string payload)
    {
        if (string.IsNullOrEmpty(payload) || payload.StartsWith("http", StringComparison.Ordinal))
        {
            return Uri.TryCreate(payload, UriKind.Absolute, out var direct) ? direct : null;
        }

        var body = payload.StartsWith('u') && payload.Length > 1 && payload[1..].StartsWith("http", StringComparison.Ordinal)
            ? payload[1..]
            : payload;

        if (body.StartsWith("http", StringComparison.Ordinal))
        {
            return Uri.TryCreate(body, UriKind.Absolute, out var full) ? full : null;
        }

        return key switch
        {
            ServiceKeys.BbcSounds => Uri.TryCreate($"https://www.bbc.co.uk/sounds/play/{body}", UriKind.Absolute, out var bs) ? bs : null,
            ServiceKeys.BbcIplayer => Uri.TryCreate($"https://www.bbc.co.uk/iplayer/episode/{body}", UriKind.Absolute, out var bi) ? bi : null,
            ServiceKeys.InternetArchive => Uri.TryCreate($"https://archive.org/details/{body}", UriKind.Absolute, out var ia) ? ia : null,
            ServiceKeys.Vimeo => Uri.TryCreate($"https://vimeo.com/{body}", UriKind.Absolute, out var v) ? v : null,
            ServiceKeys.Netflix => Uri.TryCreate($"https://www.netflix.com/title/{body}", UriKind.Absolute, out var n) ? n : null,
            _ => null
        };
    }

    private static bool IsHost(string host, string suffix) =>
        host == suffix || host.EndsWith("." + suffix, StringComparison.Ordinal);

    private static bool IsAmazonHost(string host) =>
        host == "amazon.com" || host.EndsWith(".amazon.com", StringComparison.Ordinal) ||
        host == "amazon.co.uk" || host.EndsWith(".amazon.co.uk", StringComparison.Ordinal);

    private static bool IsAmazonVideoPath(string path) =>
        path.Contains("/gp/video", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/Prime-Video", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/prime-video", StringComparison.OrdinalIgnoreCase);

    private static string? TryVimeoId(Uri url)
    {
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var candidate = parts[0] == "video" && parts.Length > 1 ? parts[1] : parts[0];
        return candidate.All(char.IsDigit) ? candidate : null;
    }

    private static string? TryTrimPrefixHostPath(
        string url,
        string[] pathPrefixes,
        bool allowSlug,
        string[]? hosts = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        if (hosts is { Length: > 0 } && !hosts.Any(h => IsHost(host, h)))
        {
            return null;
        }

        foreach (var prefix in pathPrefixes)
        {
            if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rest = uri.AbsolutePath[prefix.Length..];
            if (allowSlug)
            {
                var slash = rest.IndexOf('/');
                rest = slash >= 0 ? rest[..slash] : rest;
            }
            else if (rest.Contains('/'))
            {
                return null;
            }

            if (string.IsNullOrEmpty(rest) || rest.Contains('?') || rest.Contains('#'))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                return null;
            }

            return rest;
        }

        return null;
    }
}
