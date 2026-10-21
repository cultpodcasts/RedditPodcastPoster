namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Curator: extra YouTube channels of a news organisation already in the corpus
/// under another podcast name. Playables keep their episode ids. Apply must
/// parent those reports onto the canonical organisation, not mint a second one.
/// </summary>
public static class CatalogueNewsOrganisationChannelAliases
{
    public static readonly IReadOnlyDictionary<string, string> OrganisationNameByChannelPublisherName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AP Archive"] = "Associated Press",
            ["APT Clips"] = "APT"
        };

    public static IEnumerable<string> ExtraChannelPublisherNames =>
        OrganisationNameByChannelPublisherName.Keys;

    public static bool IsExtraChannel(string? publisherName)
    {
        var trimmed = publisherName?.Trim() ?? string.Empty;
        return trimmed.Length > 0 && OrganisationNameByChannelPublisherName.ContainsKey(trimmed);
    }

    public static string OrganisationNameFor(string? publisherName)
    {
        var trimmed = publisherName?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        return OrganisationNameByChannelPublisherName.TryGetValue(trimmed, out var organisationName)
            ? organisationName
            : trimmed;
    }
}
