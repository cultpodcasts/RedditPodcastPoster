using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.FranceTv.Matching;

public static class FranceTvUrlMatcher
{
    /// <summary>
    /// Series hub: <c>/{channel}/{show-slug}/</c>.
    /// Episode: <c>/{channel}/{show-slug}/{digits}-{slug}.html</c>.
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        IsSeriesUrl(url) || IsEpisodeUrl(url);

    public static bool IsSeriesUrl(Uri url)
    {
        if (!IsFranceTvHost(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 &&
               IsCatalogueSegment(parts[0]) &&
               IsCatalogueSegment(parts[1]) &&
               !parts[1].Contains('.', StringComparison.Ordinal);
    }

    public static bool IsEpisodeUrl(Uri url)
    {
        if (!IsFranceTvHost(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3 &&
               IsCatalogueSegment(parts[0]) &&
               IsCatalogueSegment(parts[1]) &&
               IsEpisodeFileSegment(parts[2]);
    }

    private static bool IsFranceTvHost(Uri url) =>
        ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "france.tv");

    private static bool IsCatalogueSegment(string segment) =>
        !string.IsNullOrWhiteSpace(segment) &&
        !segment.Contains('.', StringComparison.Ordinal);

    private static bool IsEpisodeFileSegment(string segment)
    {
        if (!segment.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = segment[..^".html".Length];
        var dash = stem.IndexOf('-');
        if (dash <= 0)
        {
            return false;
        }

        for (var i = 0; i < dash; i++)
        {
            if (!char.IsDigit(stem[i]))
            {
                return false;
            }
        }

        return dash < stem.Length - 1;
    }
}
