using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

/// <summary>
/// Composed streaming catalog. Provider assemblies supply
/// <see cref="IStreamingServiceRegistration"/> instances; composition
/// (<c>KnownStreamingServices</c>) calls <see cref="Use"/> at load.
/// </summary>
public static class StreamingServiceCatalog
{
    private static IReadOnlyList<IStreamingServiceRegistration> _registrations = [];
    private static Dictionary<string, IStreamingServiceRegistration> _byKey = new(StringComparer.Ordinal);
    private static string[] _imageCoalesceStreamingKeys = [];

    public static void Use(
        IReadOnlyList<IStreamingServiceRegistration> registrations,
        IReadOnlyList<string>? imageCoalesceStreamingKeys = null)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _registrations = registrations;
        _byKey = registrations.ToDictionary(r => r.Descriptor.Key, StringComparer.Ordinal);
        _imageCoalesceStreamingKeys = imageCoalesceStreamingKeys?.ToArray()
                                       ?? registrations.Select(r => r.Descriptor.Key).ToArray();
    }

    public static IReadOnlyList<ServiceCatalog.Descriptor> All =>
        [..ServiceCatalog.PodcastPlatforms, .._registrations.Select(r => r.Descriptor)];

    public static string[] SearchEncodedKeys =>
        _registrations.Select(r => r.Descriptor.Key).ToArray();

    public static string[] ImageCoalesceOrder =>
        [..ServiceCatalog.IndexIdImageOrder, .._imageCoalesceStreamingKeys];

    public static bool TryGet(string key, out ServiceCatalog.Descriptor descriptor)
    {
        foreach (var platform in ServiceCatalog.PodcastPlatforms)
        {
            if (platform.Key == key)
            {
                descriptor = platform;
                return true;
            }
        }

        if (_byKey.TryGetValue(key, out var registration))
        {
            descriptor = registration.Descriptor;
            return true;
        }

        descriptor = null!;
        return false;
    }

    public static string? TryResolveKey(Uri url)
    {
        if (!url.IsAbsoluteUri)
        {
            return null;
        }

        var podcast = ServiceCatalog.TryResolvePodcastPlatformKey(url);
        if (podcast is not null)
        {
            return podcast;
        }

        foreach (var registration in _registrations)
        {
            var key = registration.TryResolveKey(url);
            if (key is not null)
            {
                return key;
            }
        }

        return null;
    }

    public static string? ResolveOrHostKey(Uri url) =>
        TryResolveKey(url) ?? ServiceCatalog.KeyFromUnknownHost(url);

    public static string? TryCompactUrl(string key, Uri url)
    {
        if (!_byKey.TryGetValue(key, out var registration))
        {
            return null;
        }

        return registration.TryCompactUrl(url);
    }

    public static Uri CanonicalUrlOrSelf(string key, Uri url)
    {
        var compact = TryCompactUrl(key, url);
        if (compact is null)
        {
            return url;
        }

        return TryExpandCompactUrl(key, compact) ?? url;
    }

    public static Uri? TryExpandCompactUrl(string key, string payload)
    {
        if (string.IsNullOrEmpty(payload) || payload.StartsWith("http", StringComparison.Ordinal))
        {
            return Uri.TryCreate(payload, UriKind.Absolute, out var direct) ? direct : null;
        }

        var body = payload.StartsWith('u') && payload.Length > 1 &&
                   payload[1..].StartsWith("http", StringComparison.Ordinal)
            ? payload[1..]
            : payload;

        if (body.StartsWith("http", StringComparison.Ordinal))
        {
            return Uri.TryCreate(body, UriKind.Absolute, out var full) ? full : null;
        }

        if (!_byKey.TryGetValue(key, out var registration))
        {
            return null;
        }

        return registration.TryExpandCompactUrl(body);
    }
}
