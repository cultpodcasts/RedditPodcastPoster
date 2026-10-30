using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace Api.Services.Catalogue;

/// <summary>
/// Shared <c>services</c> PATCH for film, TV-show episode and news report: blank url+image removes the key;
/// otherwise the key is resolved from the URL when present (same as the episode edit API).
/// <c>ServiceLink.Language</c> (<c>lang</c>) is not part of this PATCH surface — only <c>url</c> and <c>image</c>
/// are applied; a lang-only payload (null/blank url and image) removes the key. Intentional film parity;
/// extend Apply/TryValidate if Links ever edits language.
/// </summary>
public static class PlayableServicesChangeApplier
{
    public static bool TryValidate(Dictionary<string, ServiceLink> services, out string? error)
    {
        error = null;
        foreach (var pair in services)
        {
            var url = Blank(pair.Value?.Url);
            var image = Blank(pair.Value?.Image);
            if (url is not null && !IsAbsoluteHttp(url))
            {
                error = $"services.{pair.Key}.url must be an absolute http(s) URL";
                return false;
            }

            if (image is not null && !IsAbsoluteHttp(image))
            {
                error = $"services.{pair.Key}.image must be an absolute http(s) URL";
                return false;
            }
        }

        return true;
    }

    /// <summary>Applies a services patch onto <paramref name="current"/> and returns the result (null when empty).</summary>
    public static Dictionary<string, ServiceLink>? Apply(
        Dictionary<string, ServiceLink>? current,
        Dictionary<string, ServiceLink> services)
    {
        var map = current is null
            ? null
            : new Dictionary<string, ServiceLink>(current, StringComparer.Ordinal);

        foreach (var pair in services)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            var url = Blank(pair.Value?.Url);
            var image = Blank(pair.Value?.Image);
            if (url is null && image is null)
            {
                map?.Remove(pair.Key);
                continue;
            }

            var key = url is null ? pair.Key : StreamingServiceCatalog.TryResolveKey(url) ?? pair.Key;
            map ??= new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
            if (!map.TryGetValue(key, out var link))
            {
                link = new ServiceLink();
                map[key] = link;
            }

            if (url is not null)
            {
                link.Url = url;
            }

            if (image is not null)
            {
                link.Image = image;
            }
        }

        return map is { Count: 0 } ? null : map;
    }

    private static Uri? Blank(Uri? uri) => uri is null || uri.ToString().Length == 0 ? null : uri;

    private static bool IsAbsoluteHttp(Uri uri) =>
        uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
