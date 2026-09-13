namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Podcast-platform catalog (Spotify, Apple, YouTube) and host-string helpers.
/// Streaming destinations register above Models via
/// <c>IStreamingServiceRegistration</c>.
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

    public static readonly IReadOnlyList<Descriptor> PodcastPlatforms =
    [
        new(ServiceKeys.YouTube, "YouTube", "youtube", true, true, ["youtube.com", "m.youtube.com", "music.youtube.com", "youtu.be"]),
        new(ServiceKeys.Spotify, "Spotify", "spotify", true, false, ["open.spotify.com"]),
        new(ServiceKeys.Apple, "Apple Podcasts", "apple", true, false, ["podcasts.apple.com"])
    ];

    /// <summary>
    /// Cover-art preference among index-id platforms (YouTube frame, then Spotify, Apple).
    /// Streaming keys follow in the composed catalog's image order.
    /// </summary>
    public static readonly string[] IndexIdImageOrder =
    [
        ServiceKeys.YouTube,
        ServiceKeys.Spotify,
        ServiceKeys.Apple
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

    public static bool IsIndexIdKey(string key) =>
        key is ServiceKeys.Spotify or ServiceKeys.YouTube or ServiceKeys.Apple;

    public static string? TryResolvePodcastPlatformKey(Uri url)
    {
        if (!url.IsAbsoluteUri)
        {
            return null;
        }

        var host = CanonicalHost(url);

        if (IsHost(host, "youtu.be") || IsHost(host, "youtube.com") || IsHost(host, "m.youtube.com") ||
            IsHost(host, "music.youtube.com"))
        {
            return ServiceKeys.YouTube;
        }

        if (IsHost(host, "open.spotify.com"))
        {
            return ServiceKeys.Spotify;
        }

        if (IsHost(host, "podcasts.apple.com"))
        {
            return ServiceKeys.Apple;
        }

        return null;
    }

    /// <summary>
    /// Key for a URL that is not a well-known service: a host slug usable as a JSON key
    /// (letters/digits only, e.g. <c>dailymotioncom</c>).
    /// </summary>
    public static string CanonicalHost(Uri url)
    {
        var host = url.Host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return host;
    }

    public static string? KeyFromUnknownHost(Uri url)
    {
        var host = CanonicalHost(url);

        var chars = host.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? null : new string(chars);
    }

    public static bool IsHost(string host, string suffix) =>
        host == suffix || host.EndsWith("." + suffix, StringComparison.Ordinal);
}
