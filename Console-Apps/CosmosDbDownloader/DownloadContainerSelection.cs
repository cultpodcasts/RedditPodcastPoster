namespace CosmosDbDownloader;

/// <summary>
/// Which Cosmos containers to download. Default is all; use --only or --skip to narrow.
/// Canonical names, extra aliases, and family aliases come from one catalog so a new
/// container is added in one place rather than a boolean bag plus parallel maps.
/// </summary>
public sealed class DownloadContainerSelection
{
    public const string PodcastsName = "podcasts";
    public const string EpisodesName = "episodes";
    public const string LookUpsName = "lookups";
    public const string TitleCasingName = "titlecasing";
    public const string SubjectsName = "subjects";
    public const string DiscoveryName = "discovery";
    public const string PushSubscriptionsName = "pushsubscriptions";
    public const string PeopleName = "people";
    public const string FilmsName = "films";
    public const string TvShowsName = "tvshows";
    public const string TvShowEpisodesName = "tvshowepisodes";
    public const string NewsOrganisationsName = "newsorganisations";
    public const string NewsReportsName = "newsreports";

    /// <summary>One row per Cosmos container. Extra aliases are single-container only.</summary>
    private static readonly ContainerCatalogEntry[] Catalog =
    [
        new(PodcastsName, ["podcast"]),
        new(EpisodesName, ["episode"]),
        new(LookUpsName, ["lookup", "look-ups"]),
        new(TitleCasingName, ["title-casing", "title-casing-rules", "titlecasingrules"]),
        new(SubjectsName, ["subject"]),
        new(DiscoveryName, ["discovery-results", "discoveryresults"]),
        new(PushSubscriptionsName, ["push", "push-subscriptions", "pushsubscription"]),
        new(PeopleName, ["person"]),
        new(FilmsName, ["film"]),
        new(TvShowsName, ["tvshow", "tv-show"]),
        new(TvShowEpisodesName, ["tvshowepisode", "tv-show-episodes"]),
        new(NewsOrganisationsName, ["newsorg", "newsorganisation", "news-organisation"]),
        new(NewsReportsName, ["newsreport", "news-reports"])
    ];

    /// <summary>Aliases that expand to every container in a product family (parent + playable).</summary>
    private static readonly (string Alias, IReadOnlyList<string> CanonicalNames)[] FamilyAliases =
    [
        ("tv", [TvShowsName, TvShowEpisodesName]),
        ("news", [NewsOrganisationsName, NewsReportsName])
    ];

    private static readonly Dictionary<string, IReadOnlyList<string>> Aliases = BuildAliases();

    private readonly HashSet<string> _enabled;

    private DownloadContainerSelection(HashSet<string> enabled)
    {
        _enabled = enabled;
    }

    public static readonly IReadOnlyList<string> AllNames =
        Catalog.Select(entry => entry.CanonicalName).ToArray();

    public bool Podcasts => Includes(PodcastsName);
    public bool Episodes => Includes(EpisodesName);
    public bool LookUps => Includes(LookUpsName);
    public bool TitleCasing => Includes(TitleCasingName);
    public bool Subjects => Includes(SubjectsName);
    public bool Discovery => Includes(DiscoveryName);
    public bool PushSubscriptions => Includes(PushSubscriptionsName);
    public bool People => Includes(PeopleName);
    public bool Films => Includes(FilmsName);
    public bool TvShows => Includes(TvShowsName);
    public bool TvShowEpisodes => Includes(TvShowEpisodesName);
    public bool NewsOrganisations => Includes(NewsOrganisationsName);
    public bool NewsReports => Includes(NewsReportsName);

    public IEnumerable<string> EnabledNames => AllNames.Where(Includes);

    public bool Includes(string canonicalName) =>
        _enabled.Contains(canonicalName);

    public static DownloadContainerSelection All() =>
        new(new HashSet<string>(AllNames, StringComparer.OrdinalIgnoreCase));

    public static DownloadContainerSelection FromRequest(CosmosDbDownloaderRequest request)
    {
        var only = NormaliseList(request.Only);
        var skip = NormaliseList(request.Skip);

        if (only.Count > 0 && skip.Count > 0)
        {
            throw new InvalidOperationException("Use either --only or --skip, not both.");
        }

        if (only.Count == 0 && skip.Count == 0)
        {
            return All();
        }

        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (only.Count > 0)
        {
            foreach (var name in only)
            {
                foreach (var canonical in ResolveNames(name))
                {
                    enabled.Add(canonical);
                }
            }
        }
        else
        {
            foreach (var name in AllNames)
            {
                enabled.Add(name);
            }

            foreach (var name in skip)
            {
                foreach (var canonical in ResolveNames(name))
                {
                    enabled.Remove(canonical);
                }
            }
        }

        if (enabled.Count == 0)
        {
            throw new InvalidOperationException(
                "No containers selected. Check --only / --skip against: " + string.Join(", ", AllNames));
        }

        return new DownloadContainerSelection(enabled);
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildAliases()
    {
        var aliases = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Catalog)
        {
            IReadOnlyList<string> singleton = [entry.CanonicalName];
            aliases[entry.CanonicalName] = singleton;
            foreach (var extra in entry.ExtraAliases)
            {
                aliases[extra] = singleton;
            }
        }

        foreach (var (alias, canonicalNames) in FamilyAliases)
        {
            aliases[alias] = canonicalNames;
        }

        return aliases;
    }

    private static List<string> NormaliseList(IEnumerable<string>? values) =>
        (values ?? [])
        .SelectMany(v => v.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(v => v.Length > 0)
        .ToList();

    private static IReadOnlyList<string> ResolveNames(string raw)
    {
        if (Aliases.TryGetValue(raw.Trim(), out var canonical))
        {
            return canonical;
        }

        throw new InvalidOperationException(
            $"Unknown container '{raw}'. Valid names: {string.Join(", ", AllNames)}.");
    }

    private sealed record ContainerCatalogEntry(string CanonicalName, IReadOnlyList<string> ExtraAliases);
}
