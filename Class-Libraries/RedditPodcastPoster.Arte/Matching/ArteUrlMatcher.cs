using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Arte.Matching;

public static partial class ArteUrlMatcher
{
    /// <summary>
    /// Collection hub: <c>/{lang}/videos/RC-{id}/{slug}/</c>.
    /// Programme: <c>/{lang}/videos/{6digits}-{3digits}-{letter}/{slug}/</c>.
    /// <c>{lang}</c> is any two-letter catalogue language (fr, de, en, es, pl, it, ro, …).
    /// The slug is optional. Theme shelves such as <c>/fr/videos/histoire/</c> are not catalogue items.
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        IsSeriesUrl(url) || IsEpisodeUrl(url);

    public static bool IsSeriesUrl(Uri url) =>
        TryMatch(url, out var id) && CollectionIdRegex().IsMatch(id);

    public static bool IsEpisodeUrl(Uri url) =>
        TryMatch(url, out var id) && ProgrammeIdRegex().IsMatch(id);

    private static bool TryMatch(Uri url, out string id)
    {
        id = string.Empty;
        if (ServiceCatalog.CanonicalHost(url) != "arte.tv")
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 3 or > 4)
        {
            return false;
        }

        if (!LanguageRegex().IsMatch(parts[0]) ||
            !parts[1].Equals("videos", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts.Length == 4 &&
            (parts[3].Contains('.', StringComparison.Ordinal) || string.IsNullOrWhiteSpace(parts[3])))
        {
            return false;
        }

        id = parts[2];
        return true;
    }

    [GeneratedRegex("^[a-z]{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LanguageRegex();

    [GeneratedRegex("^RC-\\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CollectionIdRegex();

    [GeneratedRegex("^\\d{6}-\\d{3}-[A-Z]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProgrammeIdRegex();
}
