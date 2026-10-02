namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Curator: Cosmo podcast rows that are TV programmes. The public TvShow name is
/// the show, not the channel we first found them on. Playables keep a
/// <c>services</c> map so one canonical <c>/tv/{slug}/{shortId}</c> page can
/// link YouTube, iPlayer, Netflix, and any other platform.
/// SABC News is the news desk and is not in this map.
/// </summary>
public static class CatalogueTvShowCanonicalNames
{
    public static readonly IReadOnlyDictionary<string, string> ShowNameByPublisherName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SABC"] = "3 Days in Dimona: African Hebrew Israelites",
            ["SABC 2"] = "Surviving Jefferey Epstein"
        };

    public static IEnumerable<string> PublisherNames => ShowNameByPublisherName.Keys;

    public static bool IsPublisherName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length > 0 && ShowNameByPublisherName.ContainsKey(trimmed);
    }

    public static string ShowNameFor(string? publisherName)
    {
        var trimmed = publisherName?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        return ShowNameByPublisherName.TryGetValue(trimmed, out var showName)
            ? showName
            : trimmed;
    }
}
