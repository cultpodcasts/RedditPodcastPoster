using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.DiscoveryPlus.Matching;

public static partial class DiscoveryPlusUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "discoveryplus.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("show", StringComparison.OrdinalIgnoreCase) ||
                parts[i].Equals("video", StringComparison.OrdinalIgnoreCase) ||
                parts[i].Equals("movie", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A series hub is <c>/show/{uuid}</c> (optional locale prefix) with no season/episode segments.
    /// Submit rejects hubs (like Netflix series title pages); paste an episode URL instead.
    /// </summary>
    public static bool IsShowHub(Uri url)
    {
        if (!IsSubmitUrl(url) || !TryGetShowId(url, out _))
        {
            return false;
        }

        return !IsEpisodePath(url);
    }

    /// <summary>
    /// Episode watch pages keep the show UUID then add season/episode path segments
    /// (<c>/show/{uuid}/s1/e1-slug/{episodeUuid}</c>).
    /// </summary>
    public static bool IsEpisodePath(Uri url)
    {
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("show", StringComparison.OrdinalIgnoreCase) &&
                i + 2 < parts.Length)
            {
                return true;
            }

            if (SeasonSegmentRegex().IsMatch(parts[i]) || EpisodeSegmentRegex().IsMatch(parts[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryGetShowId(Uri url, out string showId)
    {
        showId = string.Empty;
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!parts[i].Equals("show", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = parts[i + 1];
            if (Guid.TryParse(candidate, out _))
            {
                showId = candidate;
                return true;
            }

            // Slug-style show ids (legacy gold-rush style) are still a show key.
            if (!string.IsNullOrWhiteSpace(candidate) &&
                !SeasonSegmentRegex().IsMatch(candidate) &&
                !EpisodeSegmentRegex().IsMatch(candidate))
            {
                showId = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when a stored Discovery+ service URL belongs to the same show catalogue id
    /// (hub or any episode under that show).
    /// </summary>
    public static bool SameShow(Uri storedServiceUrl, string showId)
    {
        if (string.IsNullOrWhiteSpace(showId) || !TryGetShowId(storedServiceUrl, out var storedShowId))
        {
            return false;
        }

        return string.Equals(storedShowId, showId, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^s\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonSegmentRegex();

    [GeneratedRegex(@"^e\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeSegmentRegex();
}
