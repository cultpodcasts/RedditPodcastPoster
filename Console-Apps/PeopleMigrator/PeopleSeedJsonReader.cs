using RedditPodcastPoster.Models;
using RedditPodcastPoster.People.Factories;

namespace PeopleMigrator;

internal static class PeopleSeedJsonReader
{
    public static async Task<PeopleSeedLoadResult> LoadAsync(
        string seedPath,
        IPersonFactory personFactory,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(seedPath))
        {
            throw new FileNotFoundException($"Seed file not found: {seedPath}");
        }

        await using var stream = File.OpenRead(seedPath);
        var document = await PeopleSeedJsonWriter.DeserializeDocumentAsync(stream, cancellationToken)
            ?? throw new InvalidDataException($"Seed file is empty or invalid: {seedPath}");

        var registry = new PersonMigrationRegistry(personFactory);
        foreach (var entry in document.People)
        {
            var person = personFactory.Create(
                entry.Name,
                entry.Aliases is { Length: > 0 } ? entry.Aliases : null,
                entry.TwitterHandle,
                entry.BlueskyHandle);

            registry.Register(person);

            var metadata = registry.GetOrCreateMetadataPublic(person.Id);
            foreach (var episodeIdText in entry.SourceEpisodeIds)
            {
                if (Guid.TryParse(episodeIdText, out var episodeId))
                {
                    metadata.SourceEpisodeIds.Add(episodeId);
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.Notes))
            {
                metadata.SeedNotes = entry.Notes;
            }
        }

        return new PeopleSeedLoadResult(
            registry,
            document.SourceCache,
            document.SourceBackupPath,
            document.People.Count);
    }
}

internal sealed record PeopleSeedLoadResult(
    PersonMigrationRegistry Registry,
    string? SourceCache,
    string? SourceBackupPath,
    int PeopleCount);
