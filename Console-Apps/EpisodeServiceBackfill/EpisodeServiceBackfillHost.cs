using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Factories;

namespace EpisodeServiceBackfill;

public class EpisodeServiceBackfillHost(
    ICosmosDbContainerFactory containerFactory,
    EpisodeServiceBackfillProcessor processor,
    IEpisodeCatalogPatchSource catalogPatchSource,
    ILogger<EpisodeServiceBackfillHost> logger)
{
    private static readonly JsonSerializerOptions SliceJson = new() { WriteIndented = true };
    private static readonly object ConsoleGate = new();

    public async Task<int> Run(EpisodeServiceBackfillRequest request)
    {
        var container = containerFactory.CreateEpisodesContainer();
        if (request.SinceTs > 0)
        {
            if (request.Apply)
            {
                return await RunApplySinceTs(container, request);
            }

            return await RunClassifySinceTs(container, request);
        }

        if (request.ClassifySkips)
        {
            if (request.BeforeTs > 0)
            {
                return await RunClassifySkipsByTs(container, request);
            }

            return await RunClassifySkips(container, request);
        }

        var requestedIds = ParseIds(request.Ids);
        var showPatches = request.ShowPatches || requestedIds.Count > 0;
        var dop = Math.Max(1, request.DegreeOfParallelism);
        var spotCheckSize = Math.Max(0, request.SpotCheck);
        var patchLogPath = string.IsNullOrWhiteSpace(request.PatchLog)
            ? EpisodeServiceBackfillPatchLogWriter.DefaultFileName
            : request.PatchLog;
        using var patchLog = new EpisodeServiceBackfillPatchLogWriter(patchLogPath);
        var sampler = spotCheckSize > 0
            ? new EpisodeServiceBackfillSpotCheckSampler(spotCheckSize)
            : null;

        WriteLine($"Patch log: {patchLog.Path} (overwrite). DOP={dop} spot-check={spotCheckSize}.");

        if (request.All)
        {
            return await RunAll(container, request, dop, sampler, patchLog);
        }

        var rawDocuments = requestedIds.Count > 0
            ? await LoadByIds(container, requestedIds)
            : await ScanCandidates(container, Math.Max(1, request.Scan), Math.Max(1, request.Limit));

        if (rawDocuments.Count == 0)
        {
            logger.LogWarning("No episode documents selected.");
            return 1;
        }

        var snapshotDir = request.SnapshotDir;
        if (!string.IsNullOrWhiteSpace(snapshotDir))
        {
            Directory.CreateDirectory(snapshotDir);
        }

        if (showPatches)
        {
            WriteLine("=== BEFORE (urls / ids / services / lang / title) ===");
            foreach (var json in rawDocuments)
            {
                PrintSlice(json, "before");
                WriteSliceFile(snapshotDir, json, "before");
            }

            WriteLine("=== PATCH (services + ids only; not written unless --apply) ===");
            foreach (var json in rawDocuments)
            {
                PrintPatch(json, snapshotDir);
            }
        }
        else
        {
            WriteLine($"Selected {rawDocuments.Count} document(s). Use --show-patches to print payloads.");
        }

        var report = await processor.RunAsync(
            rawDocuments,
            apply: request.Apply,
            maxDegreeOfParallelism: dop,
            sampler: sampler,
            patchLog: patchLog);
        WriteReport(report);

        if (!request.Apply)
        {
            WriteLine("Dry-run only. Re-run with --apply to save.");
        }
        else if (showPatches)
        {
            var afterIds = EpisodeServiceDocumentMigration.SelectDocumentsToBackfill(rawDocuments)
                .Select(x => x.EpisodeId)
                .ToList();
            if (afterIds.Count == 0)
            {
                afterIds = requestedIds;
            }

            var afterDocs = await LoadByIds(container, afterIds.Count > 0 ? afterIds : ParseIdsFromRaw(rawDocuments));
            WriteLine("=== AFTER ===");
            foreach (var json in afterDocs)
            {
                PrintSlice(json, "after");
                WriteSliceFile(snapshotDir, json, "after");
            }
        }

        return await CompleteWithSpotCheck(
            container,
            sampler,
            request.Apply,
            report.Missing,
            report.Mismatches);
    }

    private async Task<int> RunAll(
        Container container,
        EpisodeServiceBackfillRequest request,
        int dop,
        EpisodeServiceBackfillSpotCheckSampler? sampler,
        EpisodeServiceBackfillPatchLogWriter patchLog)
    {
        var progressEvery = Math.Max(1, request.ProgressEvery);
        var scanned = 0;
        var candidates = 0;
        var saved = 0;
        var missing = 0;
        var mismatches = 0;
        var clock = Stopwatch.StartNew();
        WriteLine(
            request.Apply
                ? "Full-container apply: progress lines only (no patches)."
                : "Full-container dry-run: progress lines only (no patches, no writes).");

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(Math.Max(8, dop * 32))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        var producer = Task.Run(async () =>
        {
            try
            {
                await foreach (var json in QueryRawStream(container, new QueryDefinition("SELECT * FROM c")))
                {
                    var n = Interlocked.Increment(ref scanned);
                    await channel.Writer.WriteAsync(json);
                    if (n % progressEvery == 0)
                    {
                        WriteProgress(
                            n,
                            Volatile.Read(ref candidates),
                            Volatile.Read(ref saved),
                            Volatile.Read(ref missing),
                            request.Apply,
                            clock.Elapsed);
                    }
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        });

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = dop };
        var consumers = Parallel.ForEachAsync(channel.Reader.ReadAllAsync(), parallelOptions, async (json, ct) =>
        {
            if (!catalogPatchSource.TryCreate(json, out var patch) || patch is null)
            {
                return;
            }

            if (!EpisodeServiceCatalogPatchIdentity.Matches(json, patch, out var reason))
            {
                Interlocked.Increment(ref mismatches);
                logger.LogError(
                    "Episode service backfill: identity mismatch ({Reason}). Patch episode {EpisodeId} podcast {PodcastId} was not written.",
                    reason,
                    patch.EpisodeId,
                    patch.PodcastId);
                return;
            }

            Interlocked.Increment(ref candidates);
            if (!request.Apply)
            {
                sampler?.Offer(patch);
                patchLog.Write(patch, applied: false);
                return;
            }

            var written = await processor.ApplyPatchAsync(json, patch, ct);
            if (!written)
            {
                Interlocked.Increment(ref missing);
                patchLog.Write(patch, applied: false);
                return;
            }

            Interlocked.Increment(ref saved);
            sampler?.Offer(patch);
            patchLog.Write(patch, applied: true);
        });

        try
        {
            await Task.WhenAll(producer, consumers);
        }
        catch
        {
            channel.Writer.TryComplete();
            throw;
        }

        clock.Stop();
        WriteProgress(scanned, candidates, saved, missing, request.Apply, clock.Elapsed);
        WriteLine(
            $"Done. Scanned={scanned} Candidates={candidates} Saved={saved} Missing={missing} Mismatches={mismatches} Applied={request.Apply} Elapsed={FormatElapsed(clock.Elapsed)}");
        if (!request.Apply)
        {
            WriteLine("Dry-run only. Re-run with --all --apply to save.");
        }

        logger.LogInformation(
            "Episode service backfill --all complete. Scanned={Scanned} Candidates={Candidates} Saved={Saved} Missing={Missing} Mismatches={Mismatches} Applied={Applied} ElapsedMs={ElapsedMs}",
            scanned,
            candidates,
            saved,
            missing,
            mismatches,
            request.Apply,
            clock.ElapsedMilliseconds);

        return await CompleteWithSpotCheck(container, sampler, request.Apply, missing, mismatches);
    }

    private async Task<int> RunApplySinceTs(Container container, EpisodeServiceBackfillRequest request)
    {
        const string forbiddenPatchLog = "episode-service-backfill-patches.jsonl";
        var patchLogPath = string.IsNullOrWhiteSpace(request.PatchLog)
            ? "episode-service-backfill-since-ts.jsonl"
            : request.PatchLog;
        if (Path.GetFileName(patchLogPath).Equals(forbiddenPatchLog, StringComparison.OrdinalIgnoreCase))
        {
            WriteLine($"Refuse to write over {forbiddenPatchLog}; using episode-service-backfill-since-ts.jsonl.");
            patchLogPath = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(patchLogPath)) ?? ".",
                "episode-service-backfill-since-ts.jsonl");
        }

        var sinceIso = DateTimeOffset.FromUnixTimeSeconds(request.SinceTs).UtcDateTime.ToString("o");
        WriteLine($"Querying _ts > {request.SinceTs} ({sinceIso}). Apply={request.Apply}.");

        var query = new QueryDefinition("SELECT * FROM c WHERE c._ts > @ts")
            .WithParameter("@ts", request.SinceTs);
        var candidates = new List<string>();
        var hits = 0;
        await foreach (var json in QueryRawStream(container, query))
        {
            hits++;
            if (LeftoverEpisodeDocument.TryParse(json, out var leftover) &&
                leftover is not null &&
                leftover.NeedsBackfill() &&
                leftover.TryCreateCatalogPatch(out _))
            {
                candidates.Add(json);
            }
        }

        WriteLine($"Hits={hits} Candidates={candidates.Count}.");
        if (candidates.Count == 0)
        {
            WriteLine("No NeedsBackfill candidates in the _ts window.");
            return 0;
        }

        var dop = Math.Max(1, request.DegreeOfParallelism);
        var spotCheckSize = Math.Max(0, request.SpotCheck);
        using var patchLog = new EpisodeServiceBackfillPatchLogWriter(patchLogPath);
        var sampler = spotCheckSize > 0
            ? new EpisodeServiceBackfillSpotCheckSampler(Math.Min(spotCheckSize, candidates.Count))
            : null;
        WriteLine($"Patch log: {patchLog.Path} (overwrite). DOP={dop}.");

        var report = await processor.RunAsync(
            candidates,
            apply: true,
            maxDegreeOfParallelism: dop,
            sampler: sampler,
            patchLog: patchLog);
        WriteReport(report);
        return await CompleteWithSpotCheck(container, sampler, applied: true, report.Missing, report.Mismatches);
    }

    private async Task<int> RunClassifySinceTs(Container container, EpisodeServiceBackfillRequest request)
    {
        const string forbiddenPatchLog = "episode-service-backfill-patches.jsonl";
        var skipLogPath = string.IsNullOrWhiteSpace(request.PatchLog)
            ? "post-backfill-ts.jsonl"
            : request.PatchLog;
        if (Path.GetFileName(skipLogPath).Equals(forbiddenPatchLog, StringComparison.OrdinalIgnoreCase))
        {
            WriteLine($"Refuse to write over {forbiddenPatchLog}; using post-backfill-ts.jsonl.");
            skipLogPath = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(skipLogPath)) ?? ".",
                "post-backfill-ts.jsonl");
        }

        var skipDir = Path.GetDirectoryName(Path.GetFullPath(skipLogPath));
        if (!string.IsNullOrWhiteSpace(skipDir))
        {
            Directory.CreateDirectory(skipDir);
        }

        var sinceIso = DateTimeOffset.FromUnixTimeSeconds(request.SinceTs).UtcDateTime.ToString("o");
        WriteLine($"Querying _ts > {request.SinceTs} ({sinceIso}). Read-only.");

        var query = new QueryDefinition("SELECT * FROM c WHERE c._ts > @ts")
            .WithParameter("@ts", request.SinceTs);
        var hits = 0;
        var needsTrue = 0;
        var needsFalse = 0;
        var unreadable = 0;
        var stillCandidate = 0;
        await using var writer = new StreamWriter(skipLogPath, append: false, Encoding.UTF8);
        await foreach (var json in QueryRawStream(container, query))
        {
            hits++;
            Guid? episodeId = null;
            Guid? podcastId = null;
            long ts = 0;
            string classification;
            string? why = null;
            try
            {
                if (!LeftoverEpisodeDocument.TryParse(json, out var leftover) || leftover is null)
                {
                    unreadable++;
                    classification = "unreadable";
                    why = "deserialize fail";
                }
                else
                {
                    episodeId = leftover.Id == Guid.Empty ? null : leftover.Id;
                    podcastId = leftover.PodcastId == Guid.Empty ? null : leftover.PodcastId;
                    ts = leftover.Timestamp;
                    if (leftover.NeedsBackfill())
                    {
                        needsTrue++;
                        why = leftover.DescribeNeed();
                        classification = leftover.TryCreateCatalogPatch(out _)
                            ? "still_candidate"
                            : "needs_backfill_not_candidate";
                        if (classification == "still_candidate")
                        {
                            stillCandidate++;
                        }
                    }
                    else
                    {
                        needsFalse++;
                        classification = "already_complete";
                    }
                }
            }
            catch (JsonException)
            {
                unreadable++;
                classification = "unreadable";
                why = "deserialize fail";
            }

            var tsIso = ts > 0
                ? DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.ToString("o")
                : null;
            var line = JsonSerializer.Serialize(new
            {
                classification,
                needsBackfill = classification is "still_candidate" or "needs_backfill_not_candidate",
                why,
                episodeId,
                podcastId,
                ts,
                tsIso
            });
            await writer.WriteLineAsync(line);
            if (classification is not "already_complete")
            {
                WriteLine($"{episodeId} {podcastId} {tsIso} {classification} {why}");
            }
        }

        WriteLine($"Done. Hits={hits} NeedsBackfillTrue={needsTrue} NeedsBackfillFalse={needsFalse} Unreadable={unreadable} StillCandidate={stillCandidate}");
        WriteLine($"Wrote {hits} row(s) to {skipLogPath}.");
        return 0;
    }

    private async Task<int> RunClassifySkips(Container container, EpisodeServiceBackfillRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CandidatesFrom) || !File.Exists(request.CandidatesFrom))
        {
            WriteLine("classify-skips requires --candidates-from pointing at an existing patch JSONL.");
            return 1;
        }

        var skipLogPath = string.IsNullOrWhiteSpace(request.PatchLog)
            ? "episode-service-backfill-skip-reasons.jsonl"
            : request.PatchLog;
        if (Path.GetFullPath(skipLogPath) == Path.GetFullPath(request.CandidatesFrom))
        {
            WriteLine("Refuse to write skip log over --candidates-from.");
            return 1;
        }

        var candidateIds = LoadCandidateIds(request.CandidatesFrom);
        WriteLine($"Loaded {candidateIds.Count} unique candidate ids from {request.CandidatesFrom}.");

        var cosmosIds = new HashSet<Guid>();
        var scanned = 0;
        var clock = Stopwatch.StartNew();
        await foreach (var id in QueryIds(container, new QueryDefinition("SELECT VALUE c.id FROM c")))
        {
            scanned++;
            cosmosIds.Add(id);
            if (scanned % Math.Max(1, request.ProgressEvery) == 0)
            {
                WriteLine($"Progress scanned-ids={scanned} unique={cosmosIds.Count} elapsed={FormatElapsed(clock.Elapsed)}");
            }
        }

        clock.Stop();
        var skips = cosmosIds.Except(candidateIds).ToList();
        var missingFromCosmos = candidateIds.Except(cosmosIds).Count();
        WriteLine(
            $"Id scan done. Scanned={scanned} Unique={cosmosIds.Count} CandidatesFrom={candidateIds.Count} NotInPatchLog={skips.Count} PatchLogMissingFromCosmos={missingFromCosmos} Elapsed={FormatElapsed(clock.Elapsed)}");

        var skipDir = Path.GetDirectoryName(Path.GetFullPath(skipLogPath));
        if (!string.IsNullOrWhiteSpace(skipDir))
        {
            Directory.CreateDirectory(skipDir);
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var skipWriter = new StreamWriter(skipLogPath, append: false, Encoding.UTF8);
        foreach (var episodeId in skips.OrderBy(x => x))
        {
            var matches = await QueryRaw(
                container,
                new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter("@id", episodeId.ToString()));
            string reason;
            Guid? podcastId = null;
            string? title = null;
            if (matches.Count == 0)
            {
                reason = "cosmos_id_not_reloadable";
            }
            else
            {
                var json = matches[0];
                reason = LeftoverEpisodeDocument.Classify(json) ?? "now_a_candidate";
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("podcastId", out var podcastEl) &&
                    podcastEl.TryGetGuid(out var pid))
                {
                    podcastId = pid;
                }

                if (document.RootElement.TryGetProperty("title", out var titleEl) &&
                    titleEl.ValueKind == JsonValueKind.String)
                {
                    title = titleEl.GetString();
                }
            }

            counts[reason] = counts.GetValueOrDefault(reason) + 1;
            var line = JsonSerializer.Serialize(new
            {
                reason,
                episodeId,
                podcastId,
                title
            });
            await skipWriter.WriteLineAsync(line);
            WriteLine($"{episodeId} {reason} {title}");
        }

        WriteLine("Skip-reason counts:");
        foreach (var pair in counts.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal))
        {
            WriteLine($"  {pair.Key}={pair.Value}");
        }

        WriteLine($"Wrote {skips.Count} skip row(s) to {skipLogPath}.");
        return 0;
    }

    private async Task<int> RunClassifySkipsByTs(Container container, EpisodeServiceBackfillRequest request)
    {
        var skipLogPath = string.IsNullOrWhiteSpace(request.PatchLog)
            ? "episode-service-backfill-skip-reasons.jsonl"
            : request.PatchLog;
        if (!string.IsNullOrWhiteSpace(request.CandidatesFrom) &&
            Path.GetFullPath(skipLogPath) == Path.GetFullPath(request.CandidatesFrom))
        {
            WriteLine("Refuse to write skip log over --candidates-from.");
            return 1;
        }

        var skipDir = Path.GetDirectoryName(Path.GetFullPath(skipLogPath));
        if (!string.IsNullOrWhiteSpace(skipDir))
        {
            Directory.CreateDirectory(skipDir);
        }

        HashSet<Guid>? candidateIds = null;
        if (!string.IsNullOrWhiteSpace(request.CandidatesFrom) && File.Exists(request.CandidatesFrom))
        {
            candidateIds = LoadCandidateIds(request.CandidatesFrom);
            WriteLine($"Loaded {candidateIds.Count} unique candidate ids from {request.CandidatesFrom}.");
        }

        var beforeIso = DateTimeOffset.FromUnixTimeSeconds(request.BeforeTs).UtcDateTime.ToString("o");
        WriteLine($"Querying _ts < {request.BeforeTs} ({beforeIso}).");

        var query = new QueryDefinition("SELECT * FROM c WHERE c._ts < @ts")
            .WithParameter("@ts", request.BeforeTs);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var hits = 0;
        var inPatchLog = 0;
        await using var skipWriter = new StreamWriter(skipLogPath, append: false, Encoding.UTF8);
        await foreach (var json in QueryRawStream(container, query))
        {
            hits++;
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Guid? episodeId = null;
            Guid? podcastId = null;
            string? title = null;
            long ts = 0;
            if (root.TryGetProperty("id", out var idEl) && idEl.TryGetGuid(out var eid))
            {
                episodeId = eid;
            }

            if (root.TryGetProperty("podcastId", out var podcastEl) && podcastEl.TryGetGuid(out var pid))
            {
                podcastId = pid;
            }

            if (root.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
            {
                title = titleEl.GetString();
            }

            if (root.TryGetProperty("_ts", out var tsEl) && tsEl.TryGetInt64(out var unix))
            {
                ts = unix;
            }

            if (candidateIds is not null && episodeId is Guid logged && candidateIds.Contains(logged))
            {
                inPatchLog++;
            }

            var reason = LeftoverEpisodeDocument.Classify(json) ?? "now_a_candidate";
            counts[reason] = counts.GetValueOrDefault(reason) + 1;
            var tsIso = ts > 0
                ? DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.ToString("o")
                : null;
            var line = JsonSerializer.Serialize(new
            {
                reason,
                episodeId,
                podcastId,
                ts,
                tsIso,
                title
            });
            await skipWriter.WriteLineAsync(line);
            WriteLine($"{episodeId} {podcastId} {tsIso} {reason} {title}");
        }

        if (request.AfterTs > 0)
        {
            var afterIso = DateTimeOffset.FromUnixTimeSeconds(request.AfterTs).UtcDateTime.ToString("o");
            var afterCount = await QueryCount(
                container,
                new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c._ts > @ts")
                    .WithParameter("@ts", request.AfterTs));
            WriteLine($"Post-run sanity: _ts > {request.AfterTs} ({afterIso}) count={afterCount}.");
        }

        WriteLine($"_ts < {request.BeforeTs} hits={hits} alsoInPatchLog={inPatchLog}.");
        WriteLine("Skip-reason counts:");
        foreach (var pair in counts.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal))
        {
            WriteLine($"  {pair.Key}={pair.Value}");
        }

        WriteLine($"Wrote {hits} skip row(s) to {skipLogPath}.");
        return 0;
    }

    private static async Task<long> QueryCount(Container container, QueryDefinition query)
    {
        using var iterator = container.GetItemQueryStreamIterator(query);
        long total = 0;
        while (iterator.HasMoreResults)
        {
            using var response = await iterator.ReadNextAsync();
            using var payload = await JsonDocument.ParseAsync(response.Content);
            if (!payload.RootElement.TryGetProperty("Documents", out var documents) ||
                documents.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var document in documents.EnumerateArray())
            {
                if (document.ValueKind == JsonValueKind.Number && document.TryGetInt64(out var n))
                {
                    total += n;
                }
            }
        }

        return total;
    }

    private static HashSet<Guid> LoadCandidateIds(string patchLogPath)
    {
        var ids = new HashSet<Guid>();
        foreach (var line in File.ReadLines(patchLogPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("episodeId", out var idEl) &&
                idEl.TryGetGuid(out var episodeId))
            {
                ids.Add(episodeId);
            }
        }

        return ids;
    }

    private static async IAsyncEnumerable<Guid> QueryIds(Container container, QueryDefinition query)
    {
        using var iterator = container.GetItemQueryStreamIterator(query);
        while (iterator.HasMoreResults)
        {
            using var response = await iterator.ReadNextAsync();
            using var payload = await JsonDocument.ParseAsync(response.Content);
            if (!payload.RootElement.TryGetProperty("Documents", out var documents) ||
                documents.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var document in documents.EnumerateArray())
            {
                if (document.ValueKind == JsonValueKind.String && Guid.TryParse(document.GetString(), out var fromString))
                {
                    yield return fromString;
                    continue;
                }

                if (document.ValueKind == JsonValueKind.Object &&
                    document.TryGetProperty("id", out var idEl) &&
                    idEl.TryGetGuid(out var fromObject))
                {
                    yield return fromObject;
                }
            }
        }
    }

    private async Task<int> CompleteWithSpotCheck(
        Container container,
        EpisodeServiceBackfillSpotCheckSampler? sampler,
        bool applied,
        int missingPatches,
        int mismatches)
    {
        var exit = missingPatches > 0 || mismatches > 0 ? 1 : 0;
        if (sampler is null)
        {
            return exit;
        }

        var samples = sampler.Snapshot();
        if (samples.Count == 0)
        {
            WriteLine("Spot-check: sampled=0 checked=0 ok=0 mismatch=0 missing=0");
            return exit;
        }

        var ids = samples.Select(s => s.EpisodeId).Distinct().ToList();
        var loaded = await LoadByIds(container, ids);
        var storedById = new Dictionary<Guid, string>();
        foreach (var json in loaded)
        {
            if (EpisodeServiceCatalogPatchIdentity.TryRead(json, out var episodeId, out _))
            {
                storedById[episodeId] = json;
            }
        }

        var report = EpisodeServiceBackfillSpotCheckVerifier.Verify(samples, storedById, applied);
        WriteLine(
            $"Spot-check: sampled={report.Sampled} checked={report.Checked} ok={report.Ok} mismatch={report.Mismatch} missing={report.Missing}");
        foreach (var failure in report.Failures)
        {
            WriteLine($"  fail {failure.EpisodeId} podcast {failure.PodcastId}: {failure.Reason}");
        }

        if (report.Mismatch > 0 || report.Missing > 0)
        {
            return 1;
        }

        return exit;
    }

    private static void WriteLine(string message)
    {
        lock (ConsoleGate)
        {
            Console.WriteLine(message);
        }
    }

    private static void WriteProgress(
        int scanned,
        int candidates,
        int saved,
        int missing,
        bool apply,
        TimeSpan elapsed)
    {
        var rate = elapsed.TotalSeconds > 0 ? scanned / elapsed.TotalSeconds : 0;
        WriteLine(
            apply
                ? $"Progress scanned={scanned} candidates={candidates} saved={saved} missing={missing} {rate:0} docs/s elapsed={FormatElapsed(elapsed)}"
                : $"Progress scanned={scanned} candidates={candidates} {rate:0} docs/s elapsed={FormatElapsed(elapsed)}");
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalMinutes >= 1
            ? $"{elapsed.TotalMinutes:0.0}m"
            : $"{elapsed.TotalSeconds:0}s";

    private void WriteReport(EpisodeServiceBackfillReport report)
    {
        logger.LogInformation(
            "Backfill report: Candidates={Candidates} Saved={Saved} Unchanged={Unchanged} Missing={Missing} Mismatches={Mismatches} Applied={Applied}",
            report.Candidates,
            report.Saved,
            report.Unchanged,
            report.Missing,
            report.Mismatches,
            report.Applied);
        WriteLine(
            $"Report: Candidates={report.Candidates} Saved={report.Saved} Unchanged={report.Unchanged} Missing={report.Missing} Mismatches={report.Mismatches} Applied={report.Applied}");
    }

    private static List<Guid> ParseIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return [];
        }

        return ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Guid.Parse)
            .ToList();
    }

    private static List<Guid> ParseIdsFromRaw(IReadOnlyList<string> rawDocuments)
    {
        var ids = new List<Guid>();
        foreach (var json in rawDocuments)
        {
            if (EpisodeServiceCatalogPatchIdentity.TryRead(json, out var id, out _))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private async Task<List<string>> ScanCandidates(Container container, int scan, int limit)
    {
        var sql = $"SELECT TOP {scan} * FROM c WHERE IS_DEFINED(c.urls)";
        var loaded = await QueryRaw(container, new QueryDefinition(sql));
        var selected = new List<string>();
        foreach (var json in loaded)
        {
            if (!LeftoverEpisodeDocument.TryParse(json, out var leftover) ||
                leftover is null ||
                !leftover.NeedsBackfill())
            {
                continue;
            }

            selected.Add(json);
            if (selected.Count >= limit)
            {
                break;
            }
        }

        logger.LogInformation("Scanned {Scanned} documents; selected {Selected} NeedsBackfill candidates.",
            loaded.Count, selected.Count);
        return selected;
    }

    private async Task<List<string>> LoadByIds(Container container, IReadOnlyList<Guid> ids)
    {
        var found = new List<string>();
        foreach (var id in ids)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id.ToString());
            var matches = await QueryRaw(container, query);
            if (matches.Count == 0)
            {
                logger.LogWarning("Episode {EpisodeId} was not found.", id);
                continue;
            }

            found.Add(matches[0]);
        }

        return found;
    }

    private static async Task<List<string>> QueryRaw(Container container, QueryDefinition query)
    {
        var results = new List<string>();
        await foreach (var json in QueryRawStream(container, query))
        {
            results.Add(json);
        }

        return results;
    }

    private static async IAsyncEnumerable<string> QueryRawStream(Container container, QueryDefinition query)
    {
        using var iterator = container.GetItemQueryStreamIterator(query);
        while (iterator.HasMoreResults)
        {
            using var response = await iterator.ReadNextAsync();
            using var payload = await JsonDocument.ParseAsync(response.Content);
            if (!payload.RootElement.TryGetProperty("Documents", out var documents) ||
                documents.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var document in documents.EnumerateArray())
            {
                yield return document.GetRawText();
            }
        }
    }

    private void PrintPatch(string json, string? snapshotDir)
    {
        if (!catalogPatchSource.TryCreate(json, out var patch) || patch is null)
        {
            WriteLine(JsonSerializer.Serialize(new { patch = (object?)null, reason = "not a candidate" }, SliceJson));
            return;
        }

        var payload = new
        {
            patch.EpisodeId,
            patch.PodcastId,
            patch.Ids,
            patch.Services
        };
        WriteLine(JsonSerializer.Serialize(payload, SliceJson));
        if (string.IsNullOrWhiteSpace(snapshotDir))
        {
            return;
        }

        var path = Path.Combine(snapshotDir, $"{patch.EpisodeId}.patch.json");
        File.WriteAllText(path, JsonSerializer.Serialize(payload, SliceJson), Encoding.UTF8);
    }

    private static void PrintSlice(string json, string label)
    {
        using var document = JsonDocument.Parse(json);
        WriteLine(JsonSerializer.Serialize(BuildSlice(document.RootElement, label), SliceJson));
    }

    private static void WriteSliceFile(string? snapshotDir, string json, string label)
    {
        if (string.IsNullOrWhiteSpace(snapshotDir))
        {
            return;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString();
        var path = Path.Combine(snapshotDir, $"{id}.{label}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(BuildSlice(root, label), SliceJson), Encoding.UTF8);
    }

    private static Dictionary<string, JsonElement?> BuildSlice(JsonElement root, string label)
    {
        JsonElement? Prop(string name) =>
            root.TryGetProperty(name, out var value) ? value.Clone() : null;

        return new Dictionary<string, JsonElement?>
        {
            ["label"] = JsonSerializer.SerializeToElement(label),
            ["id"] = Prop("id"),
            ["podcastId"] = Prop("podcastId"),
            ["_ts"] = Prop("_ts"),
            ["title"] = Prop("title"),
            ["lang"] = Prop("lang"),
            ["description"] = Prop("description"),
            ["urls"] = Prop("urls"),
            ["ids"] = Prop("ids"),
            ["services"] = Prop("services"),
            ["spotifyId"] = Prop("spotifyId"),
            ["appleId"] = Prop("appleId"),
            ["youTubeId"] = Prop("youTubeId"),
            ["images"] = Prop("images")
        };
    }
}
