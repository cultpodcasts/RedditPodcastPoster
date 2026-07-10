using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace GuestHandleRecovery;

/// <summary>
/// Restores legacy twitterHandles / blueskyHandles on episode documents from local JSON backups.
/// Uses Cosmos patch so no other episode fields are modified.
/// </summary>
public class GuestHandleRecoveryProcessor(
    ICosmosDbContainerFactory containerFactory,
    ILogger<GuestHandleRecoveryProcessor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task Run(GuestHandleRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(request.BackupDir))
        {
            throw new DirectoryNotFoundException($"Backup directory not found: {request.BackupDir}");
        }

        logger.LogInformation("Loading guest handles from backup: {BackupDir}", request.BackupDir);
        var backupById = await LoadBackupHandlesAsync(request.BackupDir, cancellationToken);
        logger.LogInformation(
            "Backup scan complete. Episodes with guest handles: {Count}",
            backupById.Count);

        var episodesContainer = containerFactory.CreateEpisodesContainer();
        var production = await LoadProductionEpisodesAsync(episodesContainer, cancellationToken);
        logger.LogInformation(
            "Production scan complete. Total episodes: {Total}, with guest handles now: {WithHandles}",
            production.Count,
            production.Values.Count(x => x.HasGuestHandles));

        var toPatch = new List<(ProductionEpisodeDocument Prod, BackupEpisodeDocument Backup)>();
        var backupMissingInProd = 0;
        var prodAlreadyHasHandles = 0;
        var prodMissingBackupHandles = 0;

        foreach (var (id, backup) in backupById)
        {
            if (!production.TryGetValue(id, out var prod))
            {
                backupMissingInProd++;
                continue;
            }

            if (prod.HasGuestHandles)
            {
                prodAlreadyHasHandles++;
                continue;
            }

            toPatch.Add((prod, backup));
        }

        foreach (var prod in production.Values.Where(x => !x.HasGuestHandles))
        {
            if (!backupById.ContainsKey(prod.Id))
            {
                prodMissingBackupHandles++;
            }
        }

        logger.LogInformation(
            "Patch plan: {ToPatch} episode(s) to restore. " +
            "Prod already has handles: {AlreadyHas}. " +
            "Backup episodes not in prod: {BackupMissingProd}. " +
            "Prod episodes missing handles with no backup entry: {ProdMissingBackup}.",
            toPatch.Count,
            prodAlreadyHasHandles,
            backupMissingInProd,
            prodMissingBackupHandles);

        foreach (var (prod, backup) in toPatch.Take(10))
        {
            logger.LogInformation(
                "  sample {EpisodeId}: twitter=[{Twitter}] bluesky=[{Bluesky}]",
                prod.Id,
                string.Join(", ", backup.TwitterHandles ?? []),
                string.Join(", ", backup.BlueskyHandles ?? []));
        }

        if (request.DryRun)
        {
            logger.LogInformation("Dry run — no Cosmos writes performed.");
            await VerifyEpisodeAsync(episodesContainer, request.VerifyEpisodeId, cancellationToken);
            return;
        }

        var patched = 0;
        var failed = 0;
        foreach (var (prod, backup) in toPatch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await PatchGuestHandlesAsync(episodesContainer, prod, backup, cancellationToken);
                patched++;
                if (patched % 50 == 0)
                {
                    logger.LogInformation("Patched {Patched}/{Total} episodes...", patched, toPatch.Count);
                }
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(ex, "Failed to patch episode {EpisodeId}", prod.Id);
            }
        }

        logger.LogInformation(
            "Recovery complete. Patched: {Patched}, failed: {Failed}.",
            patched,
            failed);

        await VerifyEpisodeAsync(episodesContainer, request.VerifyEpisodeId, cancellationToken);
    }

    private static async Task<Dictionary<Guid, BackupEpisodeDocument>> LoadBackupHandlesAsync(
        string backupDir,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, BackupEpisodeDocument>();
        foreach (var file in Directory.EnumerateFiles(backupDir, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(file);
            var document = await JsonSerializer.DeserializeAsync<BackupEpisodeDocument>(stream, JsonOptions, cancellationToken);
            if (document == null || document.Id == Guid.Empty || !document.HasGuestHandles)
            {
                continue;
            }

            result[document.Id] = document;
        }

        return result;
    }

    private static async Task<Dictionary<Guid, ProductionEpisodeDocument>> LoadProductionEpisodesAsync(
        Container container,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, ProductionEpisodeDocument>();
        var iterator = container.GetItemQueryIterator<ProductionEpisodeDocument>(
            new QueryDefinition(
                "SELECT c.id, c.podcastId, c.twitterHandles, c.blueskyHandles, c.guests FROM c"));

        while (iterator.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var document in await iterator.ReadNextAsync())
            {
                result[document.Id] = document;
            }
        }

        return result;
    }

    private static async Task PatchGuestHandlesAsync(
        Container container,
        ProductionEpisodeDocument prod,
        BackupEpisodeDocument backup,
        CancellationToken cancellationToken)
    {
        var operations = new List<PatchOperation>();
        if (backup.TwitterHandles is { Length: > 0 })
        {
            operations.Add(PatchOperation.Set("/twitterHandles", backup.TwitterHandles));
        }

        if (backup.BlueskyHandles is { Length: > 0 })
        {
            operations.Add(PatchOperation.Set("/blueskyHandles", backup.BlueskyHandles));
        }

        if (operations.Count == 0)
        {
            return;
        }

        await container.PatchItemAsync<object>(
            prod.Id.ToString(),
            new PartitionKey(prod.PodcastId.ToString()),
            operations,
            cancellationToken: cancellationToken);
    }

    private async Task VerifyEpisodeAsync(
        Container container,
        Guid? episodeId,
        CancellationToken cancellationToken)
    {
        if (episodeId is not { } id)
        {
            return;
        }

        var iterator = container.GetItemQueryIterator<ProductionEpisodeDocument>(
            new QueryDefinition(
                    "SELECT c.id, c.podcastId, c.title, c.twitterHandles, c.blueskyHandles, c.guests FROM c WHERE c.id = @id")
                .WithParameter("@id", id.ToString()));

        ProductionEpisodeDocument? document = null;
        while (iterator.HasMoreResults)
        {
            foreach (var row in await iterator.ReadNextAsync(cancellationToken))
            {
                document = row;
                break;
            }
        }

        if (document == null)
        {
            logger.LogWarning("Verify episode {EpisodeId}: not found in production.", id);
            return;
        }

        logger.LogInformation(
            "Verify episode {EpisodeId}: twitter=[{Twitter}] bluesky=[{Bluesky}] guests={Guests}",
            document.Id,
            string.Join(", ", document.TwitterHandles ?? []),
            string.Join(", ", document.BlueskyHandles ?? []),
            document.Guests is { Length: > 0 }
                ? string.Join(", ", document.Guests)
                : "(none)");
    }
}
