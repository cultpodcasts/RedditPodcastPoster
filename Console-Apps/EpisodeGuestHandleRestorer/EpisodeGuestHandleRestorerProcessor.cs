using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace EpisodeGuestHandleRestorer;

/// <summary>
/// Restores legacy twitterHandles / blueskyHandles on episode documents from local JSON backups.
/// Uses Cosmos patch so no other episode fields are modified.
/// </summary>
public class EpisodeGuestHandleRestorerProcessor(
    ICosmosDbContainerFactory containerFactory,
    ILogger<EpisodeGuestHandleRestorerProcessor> logger)
{
    public async Task Run(EpisodeGuestHandleRestorerRequest request, CancellationToken cancellationToken = default)
    {
        var dryRun = !request.Apply;
        if (request.Apply && request.DryRun)
        {
            logger.LogWarning("--apply overrides --dry-run; writes will be performed.");
        }

        if (request.UseCache && !request.Apply)
        {
            throw new InvalidOperationException("--use-cache is only valid with --apply.");
        }

        var cachePath = GuestHandleRestoreCache.ResolveCachePath(request.CachePath, request.BackupPath);

        if (request.Apply && request.UseCache)
        {
            await RunApplyFromCacheAsync(request, cachePath, cancellationToken);
            return;
        }

        if (!Directory.Exists(request.BackupPath))
        {
            throw new DirectoryNotFoundException($"Backup directory not found: {request.BackupPath}");
        }

        await RunFullScanAsync(request, cachePath, dryRun, cancellationToken);
    }

    private async Task RunFullScanAsync(
        EpisodeGuestHandleRestorerRequest request,
        string cachePath,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading guest handles from backup: {BackupPath}", request.BackupPath);
        var backupById = await LoadBackupHandlesAsync(request.BackupPath, cancellationToken);
        logger.LogInformation(
            "Backup scan complete. Episodes with guest handles: {Count}",
            backupById.Count);

        var episodesContainer = containerFactory.CreateEpisodesContainer();
        var production = await LoadProductionEpisodesAsync(episodesContainer, backupById, cancellationToken);
        logger.LogInformation(
            "Production lookup complete. Episodes found: {Found}/{Requested}",
            production.Count,
            backupById.Count);

        var (toPatch, alreadyCorrect, notInProd) = BuildPatchPlans(backupById, production);

        logger.LogInformation(
            "Summary: backup with handles={BackupWithHandles}, would patch={WouldPatch}, already correct={AlreadyCorrect}, not in prod={NotInProd}",
            backupById.Count,
            toPatch.Count,
            alreadyCorrect,
            notInProd);

        LogDryRunReport(logger, toPatch, dryRun);

        if (dryRun)
        {
            await WritePatchCacheAsync(cachePath, request.BackupPath, toPatch, production, cancellationToken);
            logger.LogInformation(
                "Patch cache written: {CachePath} ({EntryCount} episodes to patch)",
                cachePath,
                toPatch.Count);
            logger.LogInformation("Dry run — no Cosmos writes performed.");
            return;
        }

        await ApplyPatchesAsync(episodesContainer, production, toPatch, cancellationToken);
    }

    private async Task RunApplyFromCacheAsync(
        EpisodeGuestHandleRestorerRequest request,
        string cachePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(cachePath))
        {
            logger.LogWarning(
                "Cache file not found at {CachePath}. Falling back to full backup scan.",
                cachePath);
            await RunFullScanAsync(request, cachePath, dryRun: false, cancellationToken);
            return;
        }

        logger.LogInformation("Loading patch plan from cache: {CachePath}", cachePath);
        var cache = await GuestHandleRestoreCache.ReadAsync(cachePath, cancellationToken);
        logger.LogInformation(
            "Cache loaded: {Count} entries, created {CreatedAt:u}, backup={BackupPath}",
            cache.Episodes.Count,
            cache.CreatedAt,
            cache.BackupPath);

        if (!GuestHandleRestoreCache.BackupPathMatches(cache, request.BackupPath))
        {
            logger.LogWarning(
                "Cache backup-path ({CacheBackup}) does not match --backup-path ({RequestBackup}). Proceeding with caution.",
                cache.BackupPath,
                Path.GetFullPath(request.BackupPath));
        }

        var episodesContainer = containerFactory.CreateEpisodesContainer();
        var production = await LoadProductionEpisodesFromCacheAsync(episodesContainer, cache.Episodes, cancellationToken);
        logger.LogInformation(
            "Production lookup complete. Episodes found: {Found}/{Cached}",
            production.Count,
            cache.Episodes.Count);

        var toPatch = new List<HandlePatchPlan>();
        var alreadyCorrect = 0;
        var notInProd = 0;

        foreach (var entry in cache.Episodes)
        {
            if (!production.TryGetValue(entry.EpisodeId, out var prod))
            {
                notInProd++;
                continue;
            }

            var plan = BuildPatchPlanFromCache(prod, entry);
            if (plan.PatchTwitter || plan.PatchBluesky)
            {
                toPatch.Add(plan);
            }
            else
            {
                alreadyCorrect++;
            }
        }

        logger.LogInformation(
            "Cache apply summary: cached={Cached}, still need patch={WouldPatch}, already correct={AlreadyCorrect}, not in prod={NotInProd}",
            cache.Episodes.Count,
            toPatch.Count,
            alreadyCorrect,
            notInProd);

        await ApplyPatchesAsync(episodesContainer, production, toPatch, cancellationToken);
    }

    private static (List<HandlePatchPlan> ToPatch, int AlreadyCorrect, int NotInProd) BuildPatchPlans(
        IReadOnlyDictionary<Guid, BackupEpisodeDocument> backupById,
        IReadOnlyDictionary<Guid, ProductionEpisodeDocument> production)
    {
        var toPatch = new List<HandlePatchPlan>();
        var alreadyCorrect = 0;
        var notInProd = 0;

        foreach (var (id, backup) in backupById)
        {
            if (!production.TryGetValue(id, out var prod))
            {
                notInProd++;
                continue;
            }

            var plan = BuildPatchPlan(prod, backup);
            if (plan.PatchTwitter || plan.PatchBluesky)
            {
                toPatch.Add(plan);
            }
            else
            {
                alreadyCorrect++;
            }
        }

        return (toPatch, alreadyCorrect, notInProd);
    }

    private static async Task WritePatchCacheAsync(
        string cachePath,
        string backupPath,
        IReadOnlyList<HandlePatchPlan> toPatch,
        IReadOnlyDictionary<Guid, ProductionEpisodeDocument> production,
        CancellationToken cancellationToken)
    {
        var cache = GuestHandleRestoreCache.FromPatchPlans(backupPath, toPatch, production);
        await GuestHandleRestoreCache.WriteAsync(cachePath, cache, cancellationToken);
    }

    private async Task ApplyPatchesAsync(
        Container episodesContainer,
        IReadOnlyDictionary<Guid, ProductionEpisodeDocument> production,
        IReadOnlyList<HandlePatchPlan> toPatch,
        CancellationToken cancellationToken)
    {
        if (toPatch.Count == 0)
        {
            logger.LogInformation("No episodes to patch.");
            return;
        }

        var progress = new PercentProgressReporter("Patching");
        var patched = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var plan in toPatch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!production.TryGetValue(plan.EpisodeId, out var prod))
            {
                skipped++;
                progress.Report(patched + failed + skipped, toPatch.Count);
                continue;
            }

            try
            {
                await PatchGuestHandlesAsync(episodesContainer, prod, plan, cancellationToken);
                patched++;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(ex, "Failed to patch episode {EpisodeId}", plan.EpisodeId);
            }

            progress.Report(patched + failed + skipped, toPatch.Count);
        }

        progress.Report(toPatch.Count, toPatch.Count, force: true);
        logger.LogInformation(
            "Recovery complete. Patched: {Patched}, failed: {Failed}, skipped: {Skipped}.",
            patched,
            failed,
            skipped);
    }

    private static HandlePatchPlan BuildPatchPlan(ProductionEpisodeDocument prod, BackupEpisodeDocument backup)
    {
        var patchTwitter = backup.TwitterHandles is { Length: > 0 } &&
                           !HandlesEqual(prod.TwitterHandles, backup.TwitterHandles);
        var patchBluesky = backup.BlueskyHandles is { Length: > 0 } &&
                           !HandlesEqual(prod.BlueskyHandles, backup.BlueskyHandles);

        return new HandlePatchPlan(
            prod.Id,
            backup.Title ?? prod.Title,
            prod.TwitterHandles,
            prod.BlueskyHandles,
            backup.TwitterHandles,
            backup.BlueskyHandles,
            patchTwitter,
            patchBluesky);
    }

    private static HandlePatchPlan BuildPatchPlanFromCache(ProductionEpisodeDocument prod, CachedPatchEntry entry)
    {
        var patchTwitter = entry.PatchTwitter &&
                           entry.TwitterHandles is { Length: > 0 } &&
                           !HandlesEqual(prod.TwitterHandles, entry.TwitterHandles);
        var patchBluesky = entry.PatchBluesky &&
                           entry.BlueskyHandles is { Length: > 0 } &&
                           !HandlesEqual(prod.BlueskyHandles, entry.BlueskyHandles);

        return new HandlePatchPlan(
            prod.Id,
            entry.Title ?? prod.Title,
            prod.TwitterHandles,
            prod.BlueskyHandles,
            entry.TwitterHandles,
            entry.BlueskyHandles,
            patchTwitter,
            patchBluesky);
    }

    private static bool HandlesEqual(string[]? left, string[]? right) =>
        (left ?? []).SequenceEqual(right ?? []);

    private static string FormatHandles(string[]? handles) =>
        handles is { Length: > 0 } ? string.Join(", ", handles) : "(none)";

    private static void LogDryRunReport(ILogger logger, List<HandlePatchPlan> toPatch, bool dryRun)
    {
        if (toPatch.Count == 0)
        {
            logger.LogInformation("No episodes would be patched.");
            return;
        }

        var sampleCount = Math.Min(toPatch.Count, 20);
        logger.LogInformation("Episodes to change ({Showing} of {Total}):", sampleCount, toPatch.Count);
        logger.LogInformation(
            "{EpisodeId,-38} | {Title,-50} | {ProdTwitter,-25} | {ProdBluesky,-25} | {BackupTwitter,-25} | {BackupBluesky,-25} | {WouldWrite}",
            "EpisodeId",
            "Title",
            "Prod twitter",
            "Prod bluesky",
            "Backup twitter",
            "Backup bluesky",
            "Would write");

        foreach (var plan in toPatch.Take(sampleCount))
        {
            var wouldWrite = DescribePatch(plan);
            var title = Truncate(plan.Title, 50);
            logger.LogInformation(
                "{EpisodeId,-38} | {Title,-50} | {ProdTwitter,-25} | {ProdBluesky,-25} | {BackupTwitter,-25} | {BackupBluesky,-25} | {WouldWrite}",
                plan.EpisodeId,
                title,
                FormatHandles(plan.ProdTwitterHandles),
                FormatHandles(plan.ProdBlueskyHandles),
                FormatHandles(plan.BackupTwitterHandles),
                FormatHandles(plan.BackupBlueskyHandles),
                wouldWrite);
        }

        if (!dryRun)
        {
            return;
        }

        logger.LogInformation(
            "Dry-run detail: each row shows prod guest fields, backup guest fields, and patch operations that would run.");
    }

    private static string DescribePatch(HandlePatchPlan plan)
    {
        var parts = new List<string>();
        if (plan.PatchTwitter)
        {
            parts.Add($"twitterHandles={FormatHandles(plan.BackupTwitterHandles)}");
        }

        if (plan.PatchBluesky)
        {
            parts.Add($"blueskyHandles={FormatHandles(plan.BackupBlueskyHandles)}");
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "(none)";
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(no title)";
        }

        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }

    private static async Task<Dictionary<Guid, BackupEpisodeDocument>> LoadBackupHandlesAsync(
        string backupPath,
        CancellationToken cancellationToken)
    {
        var result = new ConcurrentDictionary<Guid, BackupEpisodeDocument>();
        var files = Directory.EnumerateFiles(backupPath, "*.json").ToArray();
        var total = files.Length;
        var processed = 0;
        var progress = new PercentProgressReporter("Backup scan");

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (file, token) =>
            {
                try
                {
                    if (!Guid.TryParse(Path.GetFileNameWithoutExtension(file), out var id) || id == Guid.Empty)
                    {
                        return;
                    }

                    if (!FileContainsUtf8(file, "\"twitterHandles\"") &&
                        !FileContainsUtf8(file, "\"blueskyHandles\""))
                    {
                        return;
                    }

                    var text = await File.ReadAllTextAsync(file, token);
                    using var document = JsonDocument.Parse(text);
                    var root = document.RootElement;

                    var twitterHandles = ReadStringArray(root, "twitterHandles");
                    var blueskyHandles = ReadStringArray(root, "blueskyHandles");
                    if (twitterHandles is not { Length: > 0 } && blueskyHandles is not { Length: > 0 })
                    {
                        return;
                    }

                    Guid podcastId = Guid.Empty;
                    if (root.TryGetProperty("podcastId", out var podcastIdElement) &&
                        Guid.TryParse(podcastIdElement.GetString(), out var parsedPodcastId))
                    {
                        podcastId = parsedPodcastId;
                    }

                    result[id] = new BackupEpisodeDocument
                    {
                        Id = id,
                        PodcastId = podcastId,
                        Title = root.TryGetProperty("title", out var titleElement)
                            ? titleElement.GetString()
                            : null,
                        TwitterHandles = twitterHandles,
                        BlueskyHandles = blueskyHandles
                    };
                }
                finally
                {
                    var current = Interlocked.Increment(ref processed);
                    progress.Report(current, total);
                }
            });

        progress.Report(total, total, force: true);
        return result.ToDictionary();
    }

    private static bool FileContainsUtf8(string path, string value)
    {
        var needle = System.Text.Encoding.UTF8.GetBytes(value);
        using var stream = File.OpenRead(path);
        var buffer = new byte[64 * 1024 + needle.Length];
        var carry = 0;

        while (true)
        {
            var read = stream.Read(buffer, carry, buffer.Length - carry);
            if (read == 0)
            {
                return false;
            }

            var total = carry + read;
            if (buffer.AsSpan(0, total).IndexOf(needle) >= 0)
            {
                return true;
            }

            carry = Math.Min(needle.Length - 1, total);
            if (carry > 0)
            {
                buffer.AsSpan(total - carry, carry).CopyTo(buffer);
            }
        }
    }

    private static string[]? ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var arrayElement) ||
            arrayElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<string>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }

        return values.Count > 0 ? values.ToArray() : null;
    }

    private static async Task<Dictionary<Guid, ProductionEpisodeDocument>> LoadProductionEpisodesAsync(
        Container container,
        IReadOnlyDictionary<Guid, BackupEpisodeDocument> backupById,
        CancellationToken cancellationToken)
    {
        if (backupById.Count == 0)
        {
            return new Dictionary<Guid, ProductionEpisodeDocument>();
        }

        var result = new ConcurrentDictionary<Guid, ProductionEpisodeDocument>();
        var total = backupById.Count;
        var processed = 0;
        var progress = new PercentProgressReporter("Production compare");

        await Parallel.ForEachAsync(
            backupById,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 16
            },
            async (entry, token) =>
            {
                try
                {
                    var backup = entry.Value;
                    if (backup.PodcastId == Guid.Empty)
                    {
                        return;
                    }

                    try
                    {
                        var response = await container.ReadItemAsync<ProductionEpisodeDocument>(
                            backup.Id.ToString(),
                            new PartitionKey(backup.PodcastId.ToString()),
                            cancellationToken: token);
                        result[backup.Id] = response.Resource;
                    }
                    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // Episode not in prod — counted later.
                    }
                }
                finally
                {
                    var current = Interlocked.Increment(ref processed);
                    progress.Report(current, total);
                }
            });

        progress.Report(total, total, force: true);
        return result.ToDictionary();
    }

    private static async Task<Dictionary<Guid, ProductionEpisodeDocument>> LoadProductionEpisodesFromCacheAsync(
        Container container,
        IReadOnlyList<CachedPatchEntry> cacheEntries,
        CancellationToken cancellationToken)
    {
        if (cacheEntries.Count == 0)
        {
            return new Dictionary<Guid, ProductionEpisodeDocument>();
        }

        var result = new ConcurrentDictionary<Guid, ProductionEpisodeDocument>();
        var total = cacheEntries.Count;
        var processed = 0;
        var progress = new PercentProgressReporter("Production compare");

        await Parallel.ForEachAsync(
            cacheEntries,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 16
            },
            async (entry, token) =>
            {
                try
                {
                    if (entry.PodcastId == Guid.Empty)
                    {
                        return;
                    }

                    try
                    {
                        var response = await container.ReadItemAsync<ProductionEpisodeDocument>(
                            entry.EpisodeId.ToString(),
                            new PartitionKey(entry.PodcastId.ToString()),
                            cancellationToken: token);
                        result[entry.EpisodeId] = response.Resource;
                    }
                    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // Episode not in prod — counted later.
                    }
                }
                finally
                {
                    var current = Interlocked.Increment(ref processed);
                    progress.Report(current, total);
                }
            });

        progress.Report(total, total, force: true);
        return result.ToDictionary();
    }

    private static async Task PatchGuestHandlesAsync(
        Container container,
        ProductionEpisodeDocument prod,
        HandlePatchPlan plan,
        CancellationToken cancellationToken)
    {
        var operations = new List<PatchOperation>();
        if (plan.PatchTwitter && plan.TwitterToSet is { Length: > 0 })
        {
            operations.Add(PatchOperation.Set("/twitterHandles", plan.TwitterToSet));
        }

        if (plan.PatchBluesky && plan.BlueskyToSet is { Length: > 0 })
        {
            operations.Add(PatchOperation.Set("/blueskyHandles", plan.BlueskyToSet));
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
}
