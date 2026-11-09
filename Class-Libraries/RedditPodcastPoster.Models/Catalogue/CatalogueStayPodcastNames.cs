namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Curator seed: partisan, advocacy, news-satire, news-commentary, or one-person host
/// channels stay podcasts even when the name or YouTube About looks like a news organisation.
/// News Central TV, BreakThrough News, Newsmax, Democracy Now!, Redacted,
/// Le Média, and Law&amp;Crime are outlets and are not on this list.
/// Runtime authority is the LookUps document via <see cref="CatalogueCuratorLists"/>.
/// </summary>
public static class CatalogueStayPodcastNames
{
    public static readonly IReadOnlySet<string> Names =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Crime News AEOR",
            "Daily Caller News",
            "NIUS",
            "MAGNO NEWS",
            "HW News English",
            "MintPress News",
            "Andy Ngo",
            "Ari Melber, Journalist & Attorney",
            "XENU TV",
            "Owen Report",
            "Nancy Mace",
            // Curator review 22 Oct 2026: news-commentary podcasts that stay podcasts.
            "Fayn",
            "Mail News",
            "Re: News",
            "Rob Wolchek-Investigative News Reporter",
            "Daily Cast News",
            "Touch Breaking News",
            "On Demand News",
            "National and World News",
            "Times Radio Tech"
        };

    public static bool Contains(string? publisherName)
    {
        var trimmed = publisherName?.Trim() ?? string.Empty;
        return trimmed.Length > 0 && Names.Contains(trimmed);
    }
}
