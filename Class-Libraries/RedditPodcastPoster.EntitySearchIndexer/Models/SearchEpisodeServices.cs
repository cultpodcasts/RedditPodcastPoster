// pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret

namespace RedditPodcastPoster.EntitySearchIndexer.Models; // pragma: allowlist secret

/// <summary>
/// Compact encoding for service URLs that are not reconstructed from Spotify/Apple/YouTube ids.
/// Spotify/YouTube/Apple stay as id fields; this field is <c>svc</c>.
/// Grammar: <c>key:payload|key:payload</c>. Payload is a compact id when the catalog can
/// round-trip the URL, otherwise <c>u</c> plus the full URL. Pipe in a URL is stored as %7C.
/// Clients expand with the same catalog. Empty string when none
/// (never null — Azure Search merge ignores null).
/// </summary>
public static class SearchEpisodeServices // pragma: allowlist secret
{
    public const char EntrySeparator = '|';

    public static string Compact(IReadOnlyDictionary<string, EpisodeServiceLink>? services) // pragma: allowlist secret
    {
        if (services is null || services.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var encoded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in ServiceCatalog.SearchEncodedKeys)
        {
            if (!services.TryGetValue(key, out var link) || link.Url is null)
            {
                continue;
            }

            parts.Add(Encode(key, link.Url));
            encoded.Add(key);
        }

        foreach (var pair in services.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (ServiceCatalog.IsIndexIdKey(pair.Key) || encoded.Contains(pair.Key))
            {
                continue;
            }

            if (pair.Value.Url is null)
            {
                continue;
            }

            parts.Add(Encode(pair.Key, pair.Value.Url));
        }

        return string.Join(EntrySeparator, parts);
    }

    public static IReadOnlyList<(string Key, Uri Url)> Expand(string? svc)
    {
        if (string.IsNullOrEmpty(svc))
        {
            return [];
        }

        var results = new List<(string Key, Uri Url)>();
        foreach (var entry in svc.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = entry.IndexOf(':');
            if (colon <= 0 || colon == entry.Length - 1)
            {
                continue;
            }

            var key = entry[..colon];
            var payload = Unescape(entry[(colon + 1)..]);
            var url = ServiceCatalog.TryExpandCompactUrl(key, payload);
            if (url is not null)
            {
                results.Add((key, url));
            }
        }

        return results;
    }

    private static string Encode(string key, Uri url)
    {
        var compact = ServiceCatalog.TryCompactUrl(key, url);
        if (compact is not null)
        {
            var expanded = ServiceCatalog.TryExpandCompactUrl(key, compact);
            if (expanded is not null && expanded.ToString() == url.ToString())
            {
                return key + ":" + compact;
            }
        }

        return key + ":u" + Escape(url.ToString());
    }

    private static string Escape(string value) =>
        value.Replace("%", "%25", StringComparison.Ordinal).Replace("|", "%7C", StringComparison.Ordinal);

    private static string Unescape(string value) =>
        value.Replace("%7C", "|", StringComparison.Ordinal).Replace("%25", "%", StringComparison.Ordinal);
}
