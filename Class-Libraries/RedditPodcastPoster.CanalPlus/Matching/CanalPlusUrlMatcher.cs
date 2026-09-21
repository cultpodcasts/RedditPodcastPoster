using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.CanalPlus.Matching;

public static partial class CanalPlusUrlMatcher
{
    private static readonly HashSet<string> CatalogueKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "series", "cinema", "documentaires", "emissions", "sport", "kids", "films", "film"
    };

    /// <summary>
    /// Catalogue item: <c>/{optional-locale}/{kind}/{slug}/h/{id}</c>
    /// where <c>kind</c> is series, cinema, documentaires, emissions, sport, kids, or films.
    /// Browse shelves without <c>/h/{id}</c> are rejected.
    /// </summary>
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "canalplus.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
        {
            return false;
        }

        var index = 0;
        if (LocaleRegex().IsMatch(parts[0]))
        {
            index = 1;
        }

        if (parts.Length < index + 4)
        {
            return false;
        }

        return CatalogueKinds.Contains(parts[index]) &&
               IsCatalogueSegment(parts[index + 1]) &&
               parts[index + 2].Equals("h", StringComparison.OrdinalIgnoreCase) &&
               ContentIdRegex().IsMatch(parts[index + 3]);
    }

    private static bool IsCatalogueSegment(string segment) =>
        !string.IsNullOrWhiteSpace(segment) &&
        !segment.Contains('.', StringComparison.Ordinal);

    [GeneratedRegex("^[a-z]{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocaleRegex();

    [GeneratedRegex(@"^\d+(?:_\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ContentIdRegex();
}
