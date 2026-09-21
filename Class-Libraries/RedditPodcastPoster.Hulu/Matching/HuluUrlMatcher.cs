using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Hulu.Matching;

public static class HuluUrlMatcher
{
    /// <summary>
    /// Series hub: <c>/series/{slug}</c>.
    /// Film hub: <c>/movie/{slug}</c>.
    /// Watch page: <c>/watch/{id}</c>.
    /// Category shelves such as <c>/series</c> or <c>/hub/...</c> are not catalogue items.
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        IsSeriesUrl(url) || IsMovieUrl(url) || IsWatchUrl(url);

    public static bool IsSeriesUrl(Uri url) =>
        TryMatchKind(url, "series", out _);

    public static bool IsMovieUrl(Uri url) =>
        TryMatchKind(url, "movie", out _);

    public static bool IsWatchUrl(Uri url) =>
        TryMatchKind(url, "watch", out var id) &&
        !id.Equals("offers", StringComparison.OrdinalIgnoreCase);

    private static bool TryMatchKind(Uri url, string kind, out string id)
    {
        id = string.Empty;
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "hulu.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 ||
            !parts[0].Equals(kind, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parts[1]) ||
            parts[1].Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        id = parts[1];
        return true;
    }
}
