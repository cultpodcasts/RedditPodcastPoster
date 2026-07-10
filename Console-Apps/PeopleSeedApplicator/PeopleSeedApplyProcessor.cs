using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.People;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.People.Factories;

namespace PeopleSeedApplicator;

public class PeopleSeedApplyProcessor(
    IPersonRepository personRepository,
    IPersonFactory personFactory,
    ILogger<PeopleSeedApplyProcessor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<int> Run(PeopleSeedApplyRequest request)
    {
        var seedPath = Path.GetFullPath(request.SeedPath);
        if (!File.Exists(seedPath))
        {
            logger.LogError("Seed file not found: {SeedPath}", seedPath);
            return 1;
        }

        await using var stream = File.OpenRead(seedPath);
        var document = await JsonSerializer.DeserializeAsync<PeopleSeedDocument>(stream, JsonOptions);
        if (document?.People is null || document.People.Count == 0)
        {
            logger.LogError("Seed file has no people: {SeedPath}", seedPath);
            return 1;
        }

        var seedEntries = document.People
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => Person.NormalizeNameKey(p.Name))
            .Select(g => g.First())
            .ToList();

        if (seedEntries.Count != document.People.Count)
        {
            logger.LogWarning(
                "Seed had {RawCount} entries; using {UniqueCount} after blank-name filter / nameKey dedupe.",
                document.People.Count,
                seedEntries.Count);
        }

        logger.LogInformation(
            "Mode: {Mode}. Seed: {SeedPath} ({SeedCount} people).",
            request.Apply ? "APPLY" : "DRY-RUN",
            seedPath,
            seedEntries.Count);

        var existingByNameKey = new Dictionary<string, Person>(StringComparer.Ordinal);
        await foreach (var person in personRepository.GetAll())
        {
            person.EnsureNameKey();
            if (string.IsNullOrEmpty(person.NameKey))
            {
                continue;
            }

            if (!existingByNameKey.TryAdd(person.NameKey, person))
            {
                logger.LogWarning(
                    "Duplicate nameKey in Cosmos People container: '{NameKey}' (keeping first id {Id}).",
                    person.NameKey,
                    existingByNameKey[person.NameKey].Id);
            }
        }

        logger.LogInformation("Cosmos People container currently has {Count} documents.", existingByNameKey.Count);

        var toCreate = 0;
        var toUpdate = 0;
        var toSkip = 0;
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failures = 0;
        var sortNameInferred = 0;
        var sortNameBackfilled = 0;
        var sampleCreate = new List<string>();
        var sampleUpdate = new List<string>();
        var sampleSkip = new List<string>();
        var sampleSortBackfill = new List<string>();

        foreach (var entry in seedEntries)
        {
            var nameKey = Person.NormalizeNameKey(entry.Name);
            var desiredAliases = NormalizeAliases(entry.Aliases);
            var desiredTwitter = PersonFactory.NormalizeHandle(entry.TwitterHandle);
            var desiredBluesky = PersonFactory.NormalizeHandle(entry.BlueskyHandle);
            // Always materialize effective sortName for Cosmos visibility.
            // Seed often omits last-token defaults (reviewer historically nulls those out).
            // Prefer explicit seed sortName; otherwise GuessSortName (org full name / last token).
            // Do not use ResolveForPersist here — that helper may omit last-token defaults.
            var desiredSortName = !string.IsNullOrWhiteSpace(entry.SortName)
                ? entry.SortName.Trim()
                : PersonSortNameResolver.GuessSortName(entry.Name);
            if (string.IsNullOrWhiteSpace(desiredSortName))
            {
                desiredSortName = null;
            }

            if (desiredSortName is not null && string.IsNullOrWhiteSpace(entry.SortName))
            {
                sortNameInferred++;
                if (sampleSortBackfill.Count < 10)
                {
                    sampleSortBackfill.Add($"{entry.Name.Trim()} → {desiredSortName}");
                }
            }

            if (!existingByNameKey.TryGetValue(nameKey, out var existing))
            {
                toCreate++;
                if (sampleCreate.Count < 5)
                {
                    sampleCreate.Add(entry.Name.Trim());
                }

                if (!request.Apply)
                {
                    continue;
                }

                try
                {
                    var person = personFactory.Create(
                        entry.Name,
                        desiredAliases,
                        desiredTwitter,
                        desiredBluesky,
                        desiredSortName);
                    await personRepository.Save(person);
                    existingByNameKey[person.NameKey] = person;
                    created++;
                }
                catch (Exception ex)
                {
                    failures++;
                    logger.LogError(ex, "Failed to create person '{Name}'.", entry.Name);
                }

                continue;
            }

            if (IsSame(existing, entry.Name.Trim(), desiredSortName, desiredAliases, desiredTwitter, desiredBluesky))
            {
                toSkip++;
                if (sampleSkip.Count < 5)
                {
                    sampleSkip.Add(existing.Name);
                }

                skipped++;
                continue;
            }

            toUpdate++;
            if (desiredSortName is not null && string.IsNullOrWhiteSpace(existing.SortName))
            {
                sortNameBackfilled++;
            }

            if (sampleUpdate.Count < 5)
            {
                sampleUpdate.Add(existing.Name);
            }

            if (!request.Apply)
            {
                continue;
            }

            try
            {
                existing.Name = entry.Name.Trim();
                existing.SortName = desiredSortName;
                existing.Aliases = desiredAliases;
                existing.TwitterHandle = desiredTwitter;
                existing.BlueskyHandle = desiredBluesky;
                existing.EnsureNameKey();
                await personRepository.Save(existing);
                updated++;
            }
            catch (Exception ex)
            {
                failures++;
                logger.LogError(ex, "Failed to update person '{Name}' (id {Id}).", existing.Name, existing.Id);
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== People seed apply summary ===");
        Console.WriteLine($"Mode:            {(request.Apply ? "APPLY" : "DRY-RUN")}");
        Console.WriteLine($"Seed people:     {seedEntries.Count}");
        Console.WriteLine($"Already exist:   {toUpdate + toSkip} (by nameKey)");
        Console.WriteLine($"Would create:    {toCreate}");
        Console.WriteLine($"Would update:    {toUpdate}");
        Console.WriteLine($"Would skip:      {toSkip} (identical)");
        Console.WriteLine($"Sort inferred:   {sortNameInferred} (seed omitted; org/full-name resolved)");
        Console.WriteLine($"Sort backfill:   {sortNameBackfilled} (Cosmos missing sortName → will set)");
        if (sampleCreate.Count > 0)
        {
            Console.WriteLine($"Sample create:   {string.Join(", ", sampleCreate)}");
        }

        if (sampleUpdate.Count > 0)
        {
            Console.WriteLine($"Sample update:   {string.Join(", ", sampleUpdate)}");
        }

        if (sampleSkip.Count > 0)
        {
            Console.WriteLine($"Sample skip:     {string.Join(", ", sampleSkip)}");
        }

        if (sampleSortBackfill.Count > 0)
        {
            Console.WriteLine($"Sample sort:     {string.Join("; ", sampleSortBackfill)}");
        }

        if (request.Apply)
        {
            Console.WriteLine($"Created:         {created}");
            Console.WriteLine($"Updated:         {updated}");
            Console.WriteLine($"Skipped:         {skipped}");
            Console.WriteLine($"Failures:        {failures}");
            Console.WriteLine($"Sort backfilled: {sortNameBackfilled}");

            var cosmosCount = await personRepository.Count();
            Console.WriteLine($"Cosmos count:    {cosmosCount}");
            logger.LogInformation(
                "Apply complete. Created={Created}, Updated={Updated}, Skipped={Skipped}, Failures={Failures}, SortBackfilled={SortBackfilled}, CosmosCount={CosmosCount}",
                created, updated, skipped, failures, sortNameBackfilled, cosmosCount);
        }
        else
        {
            Console.WriteLine("No writes performed. Re-run with --apply to upsert People.");
        }

        return failures > 0 ? 2 : 0;
    }

    private static string[]? NormalizeAliases(string[]? aliases)
    {
        if (aliases is null || aliases.Length == 0)
        {
            return null;
        }

        var normalized = aliases
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0 ? null : normalized;
    }

    private static bool IsSame(
        Person existing,
        string name,
        string? sortName,
        string[]? aliases,
        string? twitterHandle,
        string? blueskyHandle)
    {
        if (!string.Equals(existing.Name.Trim(), name, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(existing.SortName, sortName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(existing.TwitterHandle, twitterHandle, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(existing.BlueskyHandle, blueskyHandle, StringComparison.Ordinal))
        {
            return false;
        }

        return SameAliasSet(existing.Aliases, aliases);
    }

    private static bool SameAliasSet(string[]? left, string[]? right)
    {
        var a = left ?? [];
        var b = right ?? [];
        if (a.Length != b.Length)
        {
            return false;
        }

        return a.Select(x => x.Trim()).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(
                b.Select(x => x.Trim()).OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
    }
}
