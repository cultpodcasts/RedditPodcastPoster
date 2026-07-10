using Microsoft.Extensions.Logging;
using RedditPodcastPoster.People.Factories;

namespace PeopleMigrator;

/// <summary>
/// Adds aliases to an existing people-seed JSON by scanning episode title/description text.
/// Does not re-scrape X/Bluesky profiles or write Cosmos/episodes.
/// </summary>
internal sealed class AliasEnrichmentProcessor(
    IPersonFactory personFactory,
    ILogger<AliasEnrichmentProcessor> logger)
{
    public async Task<AliasEnrichmentResult> RunAsync(
        PeopleMigrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var seedPath = Path.GetFullPath(request.EnrichAliasesFrom!);
        var loadResult = await PeopleSeedJsonReader.LoadAsync(seedPath, personFactory, cancellationToken);
        var registry = loadResult.Registry;

        var cachePath = !string.IsNullOrWhiteSpace(request.CachePath)
            ? Path.GetFullPath(request.CachePath)
            : loadResult.SourceCache;

        if (string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException(
                "Alias enrichment requires guest-handle cache episodes. " +
                "Pass --cache-path or use a seed file with sourceCache.");
        }

        request.CachePath = cachePath;
        var episodes = await GuestHandleCacheReader.ReadAsync(cachePath, cancellationToken);
        logger.LogInformation(
            "Loaded {PeopleCount} person record(s) from {SeedPath} and {EpisodeCount} cache episode(s).",
            loadResult.PeopleCount,
            seedPath,
            episodes.Count);

        var backupPath = request.BackupPath;
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            backupPath = loadResult.SourceBackupPath ?? GuestHandleCacheReader.ReadBackupPath(cachePath, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(backupPath))
        {
            throw new InvalidOperationException(
                "Alias enrichment requires episode backup JSON. " +
                "Pass --backup-path or use a seed file with sourceBackupPath.");
        }

        request.BackupPath = backupPath;
        if (!Directory.Exists(backupPath))
        {
            throw new DirectoryNotFoundException($"Backup directory not found: {backupPath}");
        }

        var backupLoader = new EpisodeBackupLoader(backupPath);
        var enrichResult = PeopleFromGuestHandlesBuilder.EnrichAliases(
            episodes,
            registry,
            backupLoader);

        var outputPath = !string.IsNullOrWhiteSpace(request.OutputPath)
            ? Path.GetFullPath(request.OutputPath)
            : Path.Combine(
                Path.GetDirectoryName(seedPath) ?? Directory.GetCurrentDirectory(),
                "people-seed.iteration-4.json");

        await PeopleSeedJsonWriter.WriteAsync(
            outputPath,
            cachePath,
            backupPath,
            registry.EnumeratePersons(),
            registry,
            cancellationToken);

        var peopleWithNewAliases = enrichResult.PeopleWithNewAliases;
        logger.LogInformation(
            "Alias enrichment complete. Processed {EpisodesProcessed}/{EpisodesTotal} episode(s), " +
            "added {AliasesAdded} alias(es) for {PeopleWithAliases} person(s). Wrote {OutputPath}.",
            enrichResult.EpisodesProcessed,
            enrichResult.EpisodesTotal,
            enrichResult.AliasesAdded,
            peopleWithNewAliases,
            outputPath);

        LogAliasExamples(enrichResult.Examples, request.Sample);

        return new AliasEnrichmentResult(
            enrichResult.EpisodesProcessed,
            enrichResult.EpisodesTotal,
            enrichResult.AliasesAdded,
            peopleWithNewAliases,
            outputPath,
            enrichResult.Examples);
    }

    private void LogAliasExamples(IReadOnlyList<AliasEnrichmentExample> examples, int sample)
    {
        var preview = sample <= 0 ? examples : examples.Take(sample);
        foreach (var example in preview)
        {
            logger.LogInformation(
                "  {Name} | added: {Added} | all aliases: {Aliases}",
                example.CanonicalName,
                string.Join(", ", example.AddedAliases),
                example.AllAliases.Count == 0 ? "-" : string.Join(", ", example.AllAliases));
        }

        if (sample > 0 && examples.Count > sample)
        {
            logger.LogInformation("  ... and {Remaining} more alias example(s).", examples.Count - sample);
        }
    }
}

internal sealed record AliasEnrichmentResult(
    int EpisodesProcessed,
    int EpisodesTotal,
    int AliasesAdded,
    int PeopleWithNewAliases,
    string OutputPath,
    IReadOnlyList<AliasEnrichmentExample> Examples);

internal sealed record AliasEnrichmentExample(
    string CanonicalName,
    IReadOnlyList<string> AddedAliases,
    IReadOnlyList<string> AllAliases);
