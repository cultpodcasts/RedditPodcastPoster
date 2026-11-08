namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Curator: extra YouTube channels of a news organisation already in the corpus
/// under another podcast name. <see cref="OrganisationNameByChannelPublisherName"/>
/// maps channel podcast name → canonical organisation name (e.g. AP Archive →
/// Associated Press). Playables keep their episode ids. Migrate apply must parent
/// those reports onto the canonical organisation, not mint a second one.
/// Kind transfer reuses <see cref="OrganisationNameFor"/> for the new parent's
/// Name (and fileKey seed); submit still matches by YouTube channel id.
/// Full notes: docs/catalogue-news-organisation-channel-aliases.md.
/// </summary>
public static class CatalogueNewsOrganisationChannelAliases
{
    /// <summary>
    /// Extra channel podcast <c>Name</c> → canonical NewsOrganisation <c>Name</c>.
    /// See docs/catalogue-news-organisation-channel-aliases.md.
    /// </summary>
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
