namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Runtime snapshot of the four catalogue curator lists (S-008 allowlist, stay-podcast
/// names, TV canonical names, news channel aliases). Built from LookUps when present,
/// otherwise from the compiled static seed sets.
/// </summary>
public sealed class CatalogueCuratorLists
{
    public CatalogueCuratorLists(
        IReadOnlySet<string> newsOrganisationAllowlist,
        IReadOnlySet<string> stayPodcastPublisherNames,
        IReadOnlyDictionary<string, string> tvShowNameByPublisherName,
        IReadOnlyDictionary<string, string> organisationNameByChannelPublisherName)
    {
        NewsOrganisationAllowlist = newsOrganisationAllowlist;
        StayPodcastPublisherNames = stayPodcastPublisherNames;
        TvShowNameByPublisherName = tvShowNameByPublisherName;
        OrganisationNameByChannelPublisherName = organisationNameByChannelPublisherName;
    }

    public IReadOnlySet<string> NewsOrganisationAllowlist { get; }
    public IReadOnlySet<string> StayPodcastPublisherNames { get; }
    public IReadOnlyDictionary<string, string> TvShowNameByPublisherName { get; }
    public IReadOnlyDictionary<string, string> OrganisationNameByChannelPublisherName { get; }

    public IEnumerable<string> TvShowPublisherNames => TvShowNameByPublisherName.Keys;

    public IEnumerable<string> ExtraChannelPublisherNames => OrganisationNameByChannelPublisherName.Keys;

    public static CatalogueCuratorLists FromSeed() => new(
        CatalogueNewsOrganisationAllowlist.Names,
        CatalogueStayPodcastNames.Names,
        CatalogueTvShowCanonicalNames.ShowNameByPublisherName,
        CatalogueNewsOrganisationChannelAliases.OrganisationNameByChannelPublisherName);

    public static CatalogueCuratorLists FromLookups(
        CatalogueNewsOrganisationAllowlistLookup? allowlist,
        CatalogueStayPodcastNamesLookup? stayPodcast,
        CatalogueTvShowCanonicalNamesLookup? tvShows,
        CatalogueNewsOrganisationChannelAliasesLookup? channelAliases)
    {
        var seed = FromSeed();
        return new CatalogueCuratorLists(
            ToNameSet(allowlist?.Names, seed.NewsOrganisationAllowlist),
            ToNameSet(stayPodcast?.Names, seed.StayPodcastPublisherNames),
            ToNameMap(tvShows?.ShowNameByPublisherName, seed.TvShowNameByPublisherName),
            ToNameMap(
                channelAliases?.OrganisationNameByChannelPublisherName,
                seed.OrganisationNameByChannelPublisherName));
    }

    public bool IsAllowlisted(string? publisherName)
    {
        var trimmed = publisherName?.Trim() ?? string.Empty;
        return trimmed.Length > 0 && NewsOrganisationAllowlist.Contains(trimmed);
    }

    public bool IsStayPodcast(string? publisherName)
    {
        var trimmed = publisherName?.Trim() ?? string.Empty;
        return trimmed.Length > 0 && StayPodcastPublisherNames.Contains(trimmed);
    }

    public bool IsTvShowPublisher(string? publisherName)
    {
        var trimmed = publisherName?.Trim() ?? string.Empty;
        return trimmed.Length > 0 && TvShowNameByPublisherName.ContainsKey(trimmed);
    }

    public string ShowNameFor(string? publisherName)
    {
        var trimmed = publisherName?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        return TvShowNameByPublisherName.TryGetValue(trimmed, out var showName)
            ? showName
            : trimmed;
    }

    public bool IsExtraChannel(string? publisherName)
    {
        var trimmed = publisherName?.Trim() ?? string.Empty;
        return trimmed.Length > 0 && OrganisationNameByChannelPublisherName.ContainsKey(trimmed);
    }

    public string OrganisationNameFor(string? publisherName)
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

    private static IReadOnlySet<string> ToNameSet(IEnumerable<string>? stored, IReadOnlySet<string> seed)
    {
        if (stored is null)
        {
            return seed;
        }

        var names = stored
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return names.Count > 0 ? names : seed;
    }

    private static IReadOnlyDictionary<string, string> ToNameMap(
        IReadOnlyDictionary<string, string>? stored,
        IReadOnlyDictionary<string, string> seed)
    {
        if (stored is null || stored.Count == 0)
        {
            return seed;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in stored)
        {
            var trimmedKey = key?.Trim() ?? string.Empty;
            var trimmedValue = value?.Trim() ?? string.Empty;
            if (trimmedKey.Length == 0 || trimmedValue.Length == 0)
            {
                continue;
            }

            map[trimmedKey] = trimmedValue;
        }

        return map.Count > 0 ? map : seed;
    }
}
