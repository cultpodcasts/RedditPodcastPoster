using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Zdf.Matching;

public static class ZdfUrlMatcher
{
    private static readonly HashSet<string> ReservedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "assets", "impressum", "datenschutz", "unternehmen", "kinder", "suche",
        "nachrichten", "live", "login", "hilfe", "service", "uri", "mt2025"
    };

    /// <summary>
    /// Programme hub: <c>/{category}/{programme-slug}</c>.
    /// Episode / play: <c>/video|play/{category}/{programme}/{episode}</c>
    /// or <c>/{category}/{programme}/{episode}</c>.
    /// Marketing roots and asset paths are rejected.
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        IsProgrammeUrl(url) || IsEpisodeUrl(url);

    public static bool IsProgrammeUrl(Uri url)
    {
        if (!IsZdfHost(url) || !TryParts(url, out var parts))
        {
            return false;
        }

        return parts.Length == 2 &&
               IsCategory(parts[0]) &&
               IsSlug(parts[1]);
    }

    public static bool IsEpisodeUrl(Uri url)
    {
        if (!IsZdfHost(url) || !TryParts(url, out var parts))
        {
            return false;
        }

        if (parts.Length == 4 &&
            (parts[0].Equals("video", StringComparison.OrdinalIgnoreCase) ||
             parts[0].Equals("play", StringComparison.OrdinalIgnoreCase)) &&
            IsCategory(parts[1]) &&
            IsSlug(parts[2]) &&
            IsSlug(parts[3]))
        {
            return true;
        }

        return parts.Length == 3 &&
               IsCategory(parts[0]) &&
               IsSlug(parts[1]) &&
               IsSlug(parts[2]);
    }

    private static bool IsZdfHost(Uri url) =>
        ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "zdf.de");

    private static bool TryParts(Uri url, out string[] parts)
    {
        parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && parts[^1].EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            parts[^1] = parts[^1][..^".html".Length];
        }

        return parts.Length > 0;
    }

    private static bool IsCategory(string segment) =>
        IsSlug(segment) &&
        !ReservedRoots.Contains(segment) &&
        !segment.StartsWith("genre-", StringComparison.OrdinalIgnoreCase) &&
        !segment.StartsWith("pub-form-", StringComparison.OrdinalIgnoreCase) &&
        !segment.StartsWith("curated-collection-", StringComparison.OrdinalIgnoreCase);

    private static bool IsSlug(string segment) =>
        !string.IsNullOrWhiteSpace(segment) &&
        !segment.Contains('.', StringComparison.Ordinal);
}
