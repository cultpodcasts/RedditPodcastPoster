namespace CosmosDbDownloader;

/// <summary>
/// Which Cosmos containers to download. Default is all; use --only or --skip to narrow.
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

    public static readonly IReadOnlyList<string> AllNames =
    [
        PodcastsName,
        EpisodesName,
        LookUpsName,
        TitleCasingName,
        SubjectsName,
        DiscoveryName,
        PushSubscriptionsName,
        PeopleName
    ];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        [PodcastsName] = PodcastsName,
        ["podcast"] = PodcastsName,
        [EpisodesName] = EpisodesName,
        ["episode"] = EpisodesName,
        [LookUpsName] = LookUpsName,
        ["lookup"] = LookUpsName,
        ["look-ups"] = LookUpsName,
        [TitleCasingName] = TitleCasingName,
        ["title-casing"] = TitleCasingName,
        ["title-casing-rules"] = TitleCasingName,
        ["titlecasingrules"] = TitleCasingName,
        [SubjectsName] = SubjectsName,
        ["subject"] = SubjectsName,
        [DiscoveryName] = DiscoveryName,
        ["discovery-results"] = DiscoveryName,
        ["discoveryresults"] = DiscoveryName,
        [PushSubscriptionsName] = PushSubscriptionsName,
        ["push"] = PushSubscriptionsName,
        ["push-subscriptions"] = PushSubscriptionsName,
        ["pushsubscription"] = PushSubscriptionsName,
        [PeopleName] = PeopleName,
        ["person"] = PeopleName
    };

    public bool Podcasts { get; private init; }
    public bool Episodes { get; private init; }
    public bool LookUps { get; private init; }
    public bool TitleCasing { get; private init; }
    public bool Subjects { get; private init; }
    public bool Discovery { get; private init; }
    public bool PushSubscriptions { get; private init; }
    public bool People { get; private init; }

    public IEnumerable<string> EnabledNames
    {
        get
        {
            if (Podcasts) yield return PodcastsName;
            if (Episodes) yield return EpisodesName;
            if (LookUps) yield return LookUpsName;
            if (TitleCasing) yield return TitleCasingName;
            if (Subjects) yield return SubjectsName;
            if (Discovery) yield return DiscoveryName;
            if (PushSubscriptions) yield return PushSubscriptionsName;
            if (People) yield return PeopleName;
        }
    }

    public static DownloadContainerSelection All() => new()
    {
        Podcasts = true,
        Episodes = true,
        LookUps = true,
        TitleCasing = true,
        Subjects = true,
        Discovery = true,
        PushSubscriptions = true,
        People = true
    };

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
                enabled.Add(ResolveName(name));
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
                enabled.Remove(ResolveName(name));
            }
        }

        if (enabled.Count == 0)
        {
            throw new InvalidOperationException(
                "No containers selected. Check --only / --skip against: " + string.Join(", ", AllNames));
        }

        return new DownloadContainerSelection
        {
            Podcasts = enabled.Contains(PodcastsName),
            Episodes = enabled.Contains(EpisodesName),
            LookUps = enabled.Contains(LookUpsName),
            TitleCasing = enabled.Contains(TitleCasingName),
            Subjects = enabled.Contains(SubjectsName),
            Discovery = enabled.Contains(DiscoveryName),
            PushSubscriptions = enabled.Contains(PushSubscriptionsName),
            People = enabled.Contains(PeopleName)
        };
    }

    private static List<string> NormaliseList(IEnumerable<string>? values) =>
        (values ?? [])
        .SelectMany(v => v.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(v => v.Length > 0)
        .ToList();

    private static string ResolveName(string raw)
    {
        if (Aliases.TryGetValue(raw.Trim(), out var canonical))
        {
            return canonical;
        }

        throw new InvalidOperationException(
            $"Unknown container '{raw}'. Valid names: {string.Join(", ", AllNames)}.");
    }
}
