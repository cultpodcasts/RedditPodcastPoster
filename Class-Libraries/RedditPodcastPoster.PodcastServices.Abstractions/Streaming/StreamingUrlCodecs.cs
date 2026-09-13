using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

public static class StreamingUrlCodecs
{
    public static string? TryTrimPrefixHostPath(
        Uri url,
        string[] pathPrefixes,
        bool allowSlug,
        string[]? hosts = null)
    {
        var host = ServiceCatalog.CanonicalHost(url);
        if (hosts is { Length: > 0 } && !hosts.Any(h => ServiceCatalog.IsHost(host, h)))
        {
            return null;
        }

        foreach (var prefix in pathPrefixes)
        {
            if (!url.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rest = url.AbsolutePath[prefix.Length..];
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

            if (!string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
            {
                return null;
            }

            return rest;
        }

        return null;
    }

    public static Uri? TryCreate(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
}
