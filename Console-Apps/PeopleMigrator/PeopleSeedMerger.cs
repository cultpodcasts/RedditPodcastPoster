using System.Text.Json;
using System.Text.Json.Serialization;

namespace PeopleMigrator;

internal static class PeopleSeedMerger
{
    internal sealed record MergePairSpec(
        string Label,
        string SurvivorName,
        string? SurvivorTwitter,
        string? SurvivorBluesky,
        string? DuplicateTwitter,
        string? DuplicateBluesky,
        string? DuplicateNameForAlias = null);

    internal sealed record MergeReportEntry(
        string Label,
        string SurvivorName,
        string? Twitter,
        string? Bluesky,
        int EpisodeCount,
        int RowsRemoved);

    internal sealed record MergeSeedResult(
        int InputCount,
        int OutputCount,
        string OutputPath,
        IReadOnlyList<MergeReportEntry> Merged,
        IReadOnlyList<string> Skipped);

    public static async Task<MergeSeedResult> MergeSeedFileAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        await using var inputStream = File.OpenRead(inputPath);
        var document = await PeopleSeedJsonWriter.DeserializeDocumentAsync(inputStream, cancellationToken)
            ?? throw new InvalidOperationException($"Could not read seed document from {inputPath}.");

        var people = document.People;
        var inputCount = people.Count;
        var merged = new List<MergeReportEntry>();
        var skipped = new List<string>();

        foreach (var spec in BuildPhase1Specs())
        {
            if (!TryMergePair(people, spec, out var report))
            {
                skipped.Add($"Phase1 {spec.SurvivorName}: rows not found");
                continue;
            }

            merged.Add(report);
        }

        foreach (var spec in BuildPhase2Specs())
        {
            if (!TryMergePair(people, spec, out var report))
            {
                skipped.Add($"Phase2 {spec.Label}: rows not found or ambiguous");
                continue;
            }

            merged.Add(report);
        }

        document.People = people
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        document.GeneratedAt = DateTimeOffset.UtcNow;

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var outputStream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(
            outputStream,
            document,
            PeopleSeedMergerJson.Options,
            cancellationToken);

        return new MergeSeedResult(
            inputCount,
            people.Count,
            outputPath,
            merged,
            skipped);
    }

    private static bool TryMergePair(
        List<PeopleSeedJsonWriter.PeopleSeedEntry> people,
        MergePairSpec spec,
        out MergeReportEntry report)
    {
        report = null!;
        var survivorIndex = FindIndex(people, spec.SurvivorTwitter, spec.SurvivorBluesky, null);
        var duplicateIndex = FindIndex(people, spec.DuplicateTwitter, spec.DuplicateBluesky, spec.DuplicateNameForAlias);

        if (survivorIndex < 0 || duplicateIndex < 0 || survivorIndex == duplicateIndex)
        {
            return false;
        }

        var survivor = people[survivorIndex];
        var duplicate = people[duplicateIndex];
        var rowsRemoved = 1;

        survivor.Name = spec.SurvivorName;
        survivor.TwitterHandle = ChooseHandle(survivor.TwitterHandle, duplicate.TwitterHandle, preferFirst: true);
        survivor.BlueskyHandle = ChooseHandle(survivor.BlueskyHandle, duplicate.BlueskyHandle, preferFirst: true);

        survivor.SourceEpisodeIds = UnionEpisodeIds(survivor.SourceEpisodeIds, duplicate.SourceEpisodeIds);
        survivor.Aliases = UnionAliases(
            survivor.Aliases,
            duplicate.Aliases,
            survivor.Name,
            survivor.TwitterHandle,
            survivor.BlueskyHandle,
            spec.DuplicateNameForAlias,
            duplicate.Name);

        survivor.Notes = MergeNotes(
            survivor.Notes,
            duplicate.Notes,
            survivor.TwitterHandle,
            duplicate.TwitterHandle,
            survivor.BlueskyHandle,
            duplicate.BlueskyHandle);

        people.RemoveAt(duplicateIndex);

        report = new MergeReportEntry(
            spec.Label,
            survivor.Name,
            survivor.TwitterHandle,
            survivor.BlueskyHandle,
            survivor.SourceEpisodeIds.Count,
            rowsRemoved);
        return true;
    }

    private static int FindIndex(
        List<PeopleSeedJsonWriter.PeopleSeedEntry> people,
        string? twitter,
        string? bluesky,
        string? name)
    {
        if (!string.IsNullOrWhiteSpace(twitter))
        {
            var index = people.FindIndex(x =>
                PersonHandleNormalizer.NormalizeExactHandle(x.TwitterHandle) ==
                PersonHandleNormalizer.NormalizeExactHandle(twitter));
            if (index >= 0)
            {
                return index;
            }
        }

        if (!string.IsNullOrWhiteSpace(bluesky))
        {
            var index = people.FindIndex(x =>
                PersonHandleNormalizer.NormalizeExactHandle(x.BlueskyHandle) ==
                PersonHandleNormalizer.NormalizeExactHandle(bluesky));
            if (index >= 0)
            {
                return index;
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            return people.FindIndex(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        return -1;
    }

    private static string? ChooseHandle(string? primary, string? secondary, bool preferFirst)
    {
        if (preferFirst && !string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        return !string.IsNullOrWhiteSpace(secondary) ? secondary : primary;
    }

    private static List<string> UnionEpisodeIds(IEnumerable<string> left, IEnumerable<string> right)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<string>();
        foreach (var id in left.Concat(right))
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var trimmed = id.Trim();
            if (seen.Add(trimmed))
            {
                output.Add(trimmed);
            }
        }

        output.Sort(StringComparer.OrdinalIgnoreCase);
        return output;
    }

    private static string[] UnionAliases(
        string[] left,
        string[] right,
        string survivorName,
        string? twitter,
        string? bluesky,
        string? extraAlias,
        string duplicateName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<string>();

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var trimmed = value.Trim();
            if (trimmed.Equals(survivorName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (PersonHandleNormalizer.NormalizeExactHandle(trimmed) ==
                PersonHandleNormalizer.NormalizeExactHandle(twitter))
            {
                return;
            }

            if (PersonHandleNormalizer.NormalizeExactHandle(trimmed) ==
                PersonHandleNormalizer.NormalizeExactHandle(bluesky))
            {
                return;
            }

            if (seen.Add(trimmed))
            {
                output.Add(trimmed);
            }
        }

        foreach (var alias in left.Concat(right))
        {
            Add(alias);
        }

        Add(extraAlias);
        if (!string.IsNullOrWhiteSpace(duplicateName) &&
            !duplicateName.Equals(survivorName, StringComparison.OrdinalIgnoreCase))
        {
            Add(duplicateName);
        }

        output.Sort(StringComparer.OrdinalIgnoreCase);
        return output.ToArray();
    }

    private static string? MergeNotes(
        string? survivorNotes,
        string? duplicateNotes,
        string? survivorTwitter,
        string? duplicateTwitter,
        string? survivorBluesky,
        string? duplicateBluesky)
    {
        var parts = new List<string>();
        void AddPart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var trimmed = value.Trim();
            if (!parts.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                parts.Add(trimmed);
            }
        }

        AddPart(survivorNotes);
        AddPart(duplicateNotes);

        if (!string.IsNullOrWhiteSpace(duplicateTwitter) &&
            !string.Equals(
                PersonHandleNormalizer.NormalizeExactHandle(survivorTwitter),
                PersonHandleNormalizer.NormalizeExactHandle(duplicateTwitter),
                StringComparison.OrdinalIgnoreCase))
        {
            AddPart($"also twitter: {duplicateTwitter.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(duplicateBluesky) &&
            !string.Equals(
                PersonHandleNormalizer.NormalizeExactHandle(survivorBluesky),
                PersonHandleNormalizer.NormalizeExactHandle(duplicateBluesky),
                StringComparison.OrdinalIgnoreCase))
        {
            AddPart($"also bluesky: {duplicateBluesky.Trim()}");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static IReadOnlyList<MergePairSpec> BuildPhase1Specs() =>
    [
        new("Phase1: Ali Fortescue", "Ali Fortescue", "@AliFortescue", null, null, "@alifortescuenews.bsky.social"),
        new("Phase1: Dave Aronberg", "Dave Aronberg", "@aronberg", null, null, "@davearonberg.bsky.social"),
        new("Phase1: Ilhan Omar", "Ilhan Omar", "@Ilhan", null, null, "@repilhan.bsky.social"),
        new("Phase1: Jared Moskowitz", "Jared Moskowitz", "@JaredEMoskowitz", null, null, "@repmoskowitz.bsky.social"),
        new("Phase1: Lauren Boebert", "Lauren Boebert", "@laurenboebert", null, "@RepBoebert", null),
        new("Phase1: Luba Kassova", "Luba Kassova", "@LubaKassova", null, null, "@lubakas.bsky.social"),
        new("Phase1: Pontsho Pilane", "Pontsho Pilane", "@pontsho_pilane", null, null, "@pontsho.bsky.social"),
        new("Phase1: Robert Garcia", "Robert Garcia", "@RobertGarcia", "@robertgarcia.house.gov", "@RepRobertGarcia", null),
        new("Phase1: Tom Swarbrick", "Tom Swarbrick", "@TomSwarbrick1", null, null, "@tomswarbrick.bsky.social")
    ];

    private static IReadOnlyList<MergePairSpec> BuildPhase2Specs() =>
    [
        new("Phase2: Anderson Cooper 360° -> Anderson Cooper", "Anderson Cooper", "@andersoncooper", null, "@AC360", null, "Anderson Cooper 360°"),
        new("Phase2: The Beat with Ari Melber -> Ari Melber", "Ari Melber", "@AriMelber", "@arimelber.bsky.social", "@TheBeatWithAri", "@beatwithari.bsky.social", "The Beat with Ari Melber"),
        new("Phase2: Liz Kendall MP -> Liz Kendall", "Liz Kendall", "@leicesterliz", null, null, "@lizforleicester.bsky.social", "Liz Kendall MP")
    ];

    private static class PeopleSeedMergerJson
    {
        internal static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
    }
}
