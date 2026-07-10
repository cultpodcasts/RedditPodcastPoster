using System.Text.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace PeopleMigrator;

internal static class PeopleSeedJsonWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static async Task<PeopleSeedDocument?> DeserializeDocumentAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        return await JsonSerializer.DeserializeAsync<PeopleSeedDocument>(stream, JsonOptions, cancellationToken);
    }

    public static async Task WriteAsync(
        string outputPath,
        string? sourceCache,
        string? sourceBackupPath,
        IEnumerable<Person> people,
        PersonMigrationRegistry registry,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new PeopleSeedDocument
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            SourceCache = sourceCache,
            SourceBackupPath = sourceBackupPath,
            People = people
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => ToSeedEntry(x, registry))
                .ToList()
        };

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
    }

    private static PeopleSeedEntry ToSeedEntry(Person person, PersonMigrationRegistry registry)
    {
        CanonicalNamePromoter.ApplyToPerson(person);
        var metadata = registry.GetMetadata(person);
        var aliases = BuildAliases(person);

        return new PeopleSeedEntry
        {
            Name = person.Name,
            Aliases = aliases,
            TwitterHandle = person.TwitterHandle,
            BlueskyHandle = person.BlueskyHandle,
            SourceEpisodeIds = metadata.SourceEpisodeIds
                .OrderBy(x => x)
                .Select(x => x.ToString())
                .ToList(),
            Notes = BuildNotes(metadata)
        };
    }

    private static string[] BuildAliases(Person person)
    {
        return PersonAliasFilter.BuildAliasesForPerson(person);
    }

    public static async Task<CleanSeedResult> CleanSeedFileAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        await using var inputStream = File.OpenRead(inputPath);
        var document = await DeserializeDocumentAsync(inputStream, cancellationToken)
            ?? throw new InvalidOperationException($"Could not read seed document from {inputPath}.");

        var promotionResult = CanonicalNamePromoter.PromoteSeedEntries(document.People);

        var removed = 0;
        var removedExamples = new List<string>();
        foreach (var person in document.People)
        {
            var before = person.Aliases ?? [];
            person.Aliases = PersonAliasFilter.FilterAliases(
                before,
                person.Name,
                person.TwitterHandle,
                person.BlueskyHandle);
            removed += before.Length - person.Aliases.Length;

            foreach (var alias in before)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                var trimmed = alias.Trim();
                if (person.Aliases.Any(x => x.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                removedExamples.Add($"{person.Name}: \"{trimmed}\"");
            }
        }

        document.GeneratedAt = DateTimeOffset.UtcNow;

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var outputStream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(outputStream, document, JsonOptions, cancellationToken);

        return new CleanSeedResult(
            document.People.Count,
            promotionResult.PromotedCount,
            promotionResult.PromotionExamples,
            removed,
            removedExamples);
    }

    private static string? BuildNotes(PersonBuildMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.SeedNotes))
        {
            return metadata.SeedNotes;
        }

        var notes = new List<string>();
        {
            notes.Add($"description extracted: {metadata.DescriptionExtractedName}");
        }

        if (!string.IsNullOrWhiteSpace(metadata.TwitterApiName))
        {
            notes.Add($"twitter web: {metadata.TwitterApiName}");
        }

        if (!string.IsNullOrWhiteSpace(metadata.BlueskyApiName))
        {
            notes.Add($"bsky API: {metadata.BlueskyApiName}");
        }

        if (!string.IsNullOrWhiteSpace(metadata.ApiNameSource))
        {
            notes.Add($"canonical from {metadata.ApiNameSource} API");
        }
        else if (!string.IsNullOrWhiteSpace(metadata.DescriptionExtractedName))
        {
            notes.Add("canonical from episode description");
        }
        else if (metadata.NameResolvedFromApi)
        {
            notes.Add("canonical from API lookup");
        }

        return notes.Count == 0 ? "canonical from handle-derived name" : string.Join("; ", notes);
    }

    internal sealed class PeopleSeedDocument
    {
        [JsonPropertyName("generatedAt")]
        public DateTimeOffset GeneratedAt { get; set; }

        [JsonPropertyName("sourceCache")]
        public string? SourceCache { get; set; }

        [JsonPropertyName("sourceBackupPath")]
        public string? SourceBackupPath { get; set; }

        [JsonPropertyName("people")]
        public List<PeopleSeedEntry> People { get; set; } = [];
    }

    internal sealed class PeopleSeedEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("aliases")]
        public string[] Aliases { get; set; } = [];

        [JsonPropertyName("twitterHandle")]
        public string? TwitterHandle { get; set; }

        [JsonPropertyName("blueskyHandle")]
        public string? BlueskyHandle { get; set; }

        [JsonPropertyName("sourceEpisodeIds")]
        public List<string> SourceEpisodeIds { get; set; } = [];

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    internal sealed record CleanSeedResult(
        int PeopleCount,
        int CanonicalsPromoted,
        IReadOnlyList<string> PromotionExamples,
        int AliasesRemoved,
        IReadOnlyList<string> RemovedExamples);
}
