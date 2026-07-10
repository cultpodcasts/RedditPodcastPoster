using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.People.Factories;

namespace PeopleMigrator;

/// <summary>
/// Seeds the People register from legacy guest handles.
/// Default output is a local people-seed.json file — no Cosmos or R2 writes.
/// Episodes and podcasts are read-only at most — this tool NEVER writes to them.
/// </summary>
public class PeopleMigrationProcessor(
    ICosmosDbContainerFactory containerFactory,
    IPersonRepository personRepository,
    IPersonFactory personFactory,
    IPeoplePublisher peoplePublisher,
    IPersonDisplayNameResolver displayNameResolver,
    ILogger<PeopleMigrationProcessor> logger)
{
    public async Task Run(PeopleMigrationRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        if ((request.PersistCosmos || request.Apply) && !request.WritesCosmos)
        {
            throw new InvalidOperationException(
                "Cosmos writes require both --persist-cosmos and --apply. " +
                "Default mode writes people-seed.json only — no Cosmos or R2.");
        }

        if (request.ClearPeople && !request.WritesCosmos)
        {
            logger.LogWarning("--clear-people ignored without --persist-cosmos and --apply.");
        }

        if (request.WritesCosmos && request.ClearPeople)
        {
            await ClearPeopleContainerAsync(cancellationToken);
        }

        var registry = new PersonMigrationRegistry(personFactory);
        HashSet<Guid>? existingPersonIds = null;
        if (request.WritesCosmos || request.FromCosmos)
        {
            existingPersonIds = await SeedExistingPeopleAsync(registry, cancellationToken);
        }

        var episodes = await LoadGuestHandleEpisodesAsync(request, cancellationToken);
        logger.LogInformation(
            "Loaded {EpisodeCount} episode(s) with guest handles from {Source}.",
            episodes.Count,
            DescribeSource(request));

        var backupLoader = await CreateBackupLoaderAsync(request, cancellationToken);
        if (backupLoader != null)
        {
            logger.LogInformation(
                "Episode description enrichment enabled from backup folder {BackupPath}.",
                request.BackupPath);
        }

        var buildResult = PeopleFromGuestHandlesBuilder.Build(
            episodes,
            registry,
            existingPersonIds,
            backupLoader);
        var pendingPeople = buildResult.PendingPeople;

        if (request.NameLookup)
        {
            await ResolveDisplayNamesAsync(pendingPeople.Values, buildResult.Registry, cancellationToken);
        }

        var outputPath = ResolveOutputPath(request);
        await PeopleSeedJsonWriter.WriteAsync(
            outputPath,
            request.CachePath,
            request.BackupPath,
            pendingPeople.Values,
            buildResult.Registry,
            cancellationToken);

        LogSeedSummary(buildResult, pendingPeople.Values, outputPath, request.Sample);

        if (!request.WritesCosmos)
        {
            return;
        }

        foreach (var person in pendingPeople.Values)
        {
            await personRepository.Save(person);
        }

        await peoplePublisher.PublishPeople();

        logger.LogInformation(
            "People register persisted to Cosmos. Episodes processed: {EpisodesScanned}, people written: {PeopleWritten}. " +
            "Episode and podcast documents were NOT modified.",
            buildResult.EpisodesScanned,
            pendingPeople.Count);
    }

    private static void ValidateRequest(PeopleMigrationRequest request)
    {
        var primarySources = 0;
        if (!string.IsNullOrWhiteSpace(request.CachePath))
        {
            primarySources++;
        }

        if (!string.IsNullOrWhiteSpace(request.BackupPath) && string.IsNullOrWhiteSpace(request.CachePath))
        {
            primarySources++;
        }

        if (request.FromCosmos)
        {
            primarySources++;
        }

        if (primarySources == 0)
        {
            throw new InvalidOperationException(
                "Specify one input source: --cache-path, --backup-path, or --from-cosmos.");
        }

        if (primarySources > 1)
        {
            throw new InvalidOperationException(
                "Specify only one handle source: --cache-path, --backup-path, or --from-cosmos. " +
                "--backup-path may accompany --cache-path for description enrichment.");
        }
    }

    private static string ResolveOutputPath(PeopleMigrationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return Path.GetFullPath(request.OutputPath);
        }

        if (!string.IsNullOrWhiteSpace(request.CachePath))
        {
            var cacheDirectory = Path.GetDirectoryName(Path.GetFullPath(request.CachePath));
            return Path.Combine(cacheDirectory ?? Directory.GetCurrentDirectory(), "people-seed.json");
        }

        if (!string.IsNullOrWhiteSpace(request.BackupPath))
        {
            return Path.Combine(Path.GetFullPath(request.BackupPath), "people-seed.json");
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "people-seed.json");
    }

    private static string DescribeSource(PeopleMigrationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CachePath))
        {
            return $"cache ({request.CachePath})";
        }

        if (!string.IsNullOrWhiteSpace(request.BackupPath))
        {
            return $"backup folder ({request.BackupPath})";
        }

        return "Cosmos episodes (read-only SELECT)";
    }

    private static async Task<EpisodeBackupLoader?> CreateBackupLoaderAsync(
        PeopleMigrationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var backupPath = request.BackupPath;
        if (string.IsNullOrWhiteSpace(backupPath) && !string.IsNullOrWhiteSpace(request.CachePath))
        {
            backupPath = GuestHandleCacheReader.ReadBackupPath(request.CachePath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(backupPath))
            {
                request.BackupPath = backupPath;
            }
        }

        if (string.IsNullOrWhiteSpace(backupPath))
        {
            return null;
        }

        if (!Directory.Exists(backupPath))
        {
            throw new DirectoryNotFoundException($"Backup directory not found: {backupPath}");
        }

        return new EpisodeBackupLoader(backupPath);
    }

    private async Task<IReadOnlyList<GuestHandleEpisode>> LoadGuestHandleEpisodesAsync(
        PeopleMigrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.CachePath))
        {
            return await GuestHandleCacheReader.ReadAsync(request.CachePath, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.BackupPath))
        {
            return await BackupGuestHandleScanner.ScanAsync(request.BackupPath, cancellationToken);
        }

        return await LoadCosmosHandleEpisodesAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> SeedExistingPeopleAsync(
        PersonMigrationRegistry registry,
        CancellationToken cancellationToken)
    {
        var existing = new HashSet<Guid>();
        await foreach (var person in personRepository.GetAll())
        {
            cancellationToken.ThrowIfCancellationRequested();
            registry.Register(person);
            existing.Add(person.Id);
        }

        if (existing.Count > 0)
        {
            logger.LogInformation("Indexed {ExistingCount} existing person record(s) from People container.", existing.Count);
        }

        return existing;
    }

    private async Task<IReadOnlyList<GuestHandleEpisode>> LoadCosmosHandleEpisodesAsync(
        CancellationToken cancellationToken)
    {
        var episodesContainer = containerFactory.CreateEpisodesContainer();
        var documents = new List<GuestHandleEpisode>();
        var iterator = episodesContainer.GetItemQueryIterator<CosmosHandleDocument>(
            new QueryDefinition("SELECT c.twitterHandles, c.blueskyHandles FROM c"));

        while (iterator.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var document in await iterator.ReadNextAsync(cancellationToken))
            {
                if (document.TwitterHandles is not { Length: > 0 } &&
                    document.BlueskyHandles is not { Length: > 0 })
                {
                    continue;
                }

                documents.Add(new GuestHandleEpisode(document.TwitterHandles, document.BlueskyHandles));
            }
        }

        return documents;
    }

    private async Task ClearPeopleContainerAsync(CancellationToken cancellationToken)
    {
        var peopleContainer = containerFactory.CreatePeopleContainer();
        var deleted = 0;
        var iterator = peopleContainer.GetItemQueryIterator<PersonIdDocument>(
            new QueryDefinition("SELECT c.id FROM c"));

        while (iterator.HasMoreResults)
        {
            foreach (var document in await iterator.ReadNextAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await peopleContainer.DeleteItemAsync<Person>(
                    document.Id.ToString(),
                    new PartitionKey(document.Id.ToString()),
                    cancellationToken: cancellationToken);
                deleted++;
            }
        }

        logger.LogInformation(
            "Cleared People container only: deleted {DeletedCount} document(s). No other containers were modified.",
            deleted);
    }

    private void LogSeedSummary(
        PeopleBuildResult buildResult,
        IEnumerable<Person> people,
        string outputPath,
        int sample)
    {
        var ordered = people
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(buildResult.Registry.GetMetadata(x).DescriptionExtractedName))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var bothHandles = ordered.Count(x =>
            !string.IsNullOrWhiteSpace(x.TwitterHandle) &&
            !string.IsNullOrWhiteSpace(x.BlueskyHandle));

        var withDescriptionNames = ordered.Count(x =>
        {
            var metadata = buildResult.Registry.GetMetadata(x);
            return !string.IsNullOrWhiteSpace(metadata.DescriptionExtractedName);
        });

        var withApiNames = ordered.Count(x => buildResult.Registry.GetMetadata(x).NameResolvedFromApi);

        logger.LogInformation(
            "Wrote {PeopleCount} person record(s) to {OutputPath}. " +
            "Processed {EpisodesScanned} episode(s) with guest handles " +
            "({BothHandlesCount} with both X and Bluesky, {DescriptionNamesCount} with description-extracted names" +
            "{ApiLookupSuffix}). No Cosmos or R2 writes performed.",
            ordered.Count,
            outputPath,
            buildResult.EpisodesScanned,
            bothHandles,
            withDescriptionNames,
            withApiNames > 0 ? $", {withApiNames} with API-resolved names" : string.Empty);

        var preview = sample <= 0 ? ordered : ordered.Take(sample);
        foreach (var person in preview)
        {
            var metadata = buildResult.Registry.GetMetadata(person);
            var aliases = person.Aliases is { Length: > 0 }
                ? string.Join(", ", person.Aliases)
                : "-";
            var episodes = metadata.SourceEpisodeIds.Count > 0
                ? string.Join(", ", metadata.SourceEpisodeIds.OrderBy(x => x).Select(x => x.ToString()[..8]))
                : "-";

            logger.LogInformation(
                "  {Name} | X: {Twitter} | BSky: {Bluesky} | extracted: {Extracted} | aliases: {Aliases} | episodes: {Episodes}",
                person.Name,
                person.TwitterHandle ?? "-",
                person.BlueskyHandle ?? "-",
                metadata.DescriptionExtractedName ?? "-",
                aliases,
                episodes);
        }

        if (sample > 0 && ordered.Count > sample)
        {
            logger.LogInformation("  ... and {Remaining} more (use --sample 0 to list all).", ordered.Count - sample);
        }
    }

    private sealed class PersonIdDocument
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
    }

    private sealed class CosmosHandleDocument
    {
        [JsonPropertyName("twitterHandles")]
        public string[]? TwitterHandles { get; set; }

        [JsonPropertyName("blueskyHandles")]
        public string[]? BlueskyHandles { get; set; }
    }

    private async Task ResolveDisplayNamesAsync(
        IEnumerable<Person> people,
        PersonMigrationRegistry registry,
        CancellationToken cancellationToken)
    {
        foreach (var person in people)
        {
            var resolution = await displayNameResolver.ResolveDisplayNameAsync(
                person.TwitterHandle,
                person.BlueskyHandle,
                cancellationToken);

            registry.ApplyApiResolution(person, resolution);

            if (!string.IsNullOrWhiteSpace(resolution.ChosenName))
            {
                var apiName = resolution.ChosenName.Trim();
                var metadata = registry.GetMetadata(person);
                if (string.IsNullOrWhiteSpace(metadata.DescriptionExtractedName) ||
                    apiName.Contains(' ') ||
                    !metadata.DescriptionExtractedName.Contains(' '))
                {
                    person.Name = apiName;
                }
            }

            await Task.Delay(75, cancellationToken);
        }
    }
}
