using Microsoft.Extensions.Logging;

namespace EpisodeWireRewrite;

public sealed class EpisodeWireRewriteHost(
    IWireCutoverEpisodeStore store,
    WireCutoverProcessor processor,
    ILogger<EpisodeWireRewriteHost> logger)
{
    public async Task<int> Run(EpisodeWireRewriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Rollback)
        {
            if (!WireCutoverEvidencePaths.TryResolveRollbackJournalPath(
                    request.Journal,
                    out var rollbackJournal,
                    out var rollbackError))
            {
                logger.LogError("{Error}", rollbackError);
                return 1;
            }

            Console.Out.WriteLine(
                $"ROLLBACK mode. apply={request.Apply}. Evidence={rollbackJournal}. " +
                "Progress lines stream; restore uses Before snapshots.");
            var rollback = await processor.RollbackAsync(
                rollbackJournal,
                request.Apply,
                request.ProgressEvery,
                request.DegreeOfParallelism);
            WriteResult(rollback);
            return rollback.Failed > 0 ? 2 : 0;
        }

        var hasIds = !string.IsNullOrWhiteSpace(request.Ids);
        if (!request.All && !hasIds && request.Limit <= 0)
        {
            logger.LogError("Specify --all, --limit N, and/or --ids.");
            return 1;
        }

        var journalPath = WireCutoverEvidencePaths.ResolveMigrateJournalPath(
            request.Journal,
            DateTime.UtcNow);

        Console.Out.WriteLine(
            $"MIGRATE mode. apply={request.Apply} (default dry-run). limit={(request.Limit <= 0 ? "unlimited" : request.Limit.ToString())}.");
        Console.Out.WriteLine(
            $"Stable order: {IWireCutoverEpisodeStore.OrderedAllQuery}");
        Console.Out.WriteLine(
            $"REPAIR EVIDENCE (always written — not optional): {journalPath}");
        Console.Out.WriteLine(
            "  (+ *-affected-ids.txt, *-applied-ids.txt, *-changes.txt beside it)");
        Console.Out.WriteLine(
            "Already-migrated episodes: no action logged; does not fail the run.");
        Console.Out.WriteLine(
            "Progress: scanned / needChange / written / failed / rate / last id.");

        IAsyncEnumerable<string> docs = hasIds
            ? store.QueryByEpisodeIdsAsync(ParseIds(request.Ids!))
            : store.QueryAllRawAsync();

        var result = await processor.MigrateAsync(
            docs,
            request.Apply,
            journalPath,
            request.ProgressEvery,
            request.Limit);
        WriteResult(result);
        if (!request.Apply)
        {
            Console.Out.WriteLine(
                "Dry-run only. Re-run with --apply to write. Keep the evidence pack for rollback/repair.");
        }

        return result.Failed > 0 ? 2 : 0;
    }

    private static void WriteResult(WireCutoverRunResult result)
    {
        Console.Out.WriteLine(
            $"RESULT mode={result.Mode} apply={result.Applied} scanned={result.Scanned} " +
            $"needChange={result.NeedChange} written={result.Written} failed={result.Failed} " +
            $"unchanged={result.Unchanged} limit={result.Limit} affectedIds={result.AffectedEpisodeIds.Count}");
        if (!string.IsNullOrWhiteSpace(result.JournalPath))
        {
            Console.Out.WriteLine($"Evidence journal: {result.JournalPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.AffectedIdsPath))
        {
            Console.Out.WriteLine($"Affected ids (planned/touched): {result.AffectedIdsPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.AppliedIdsPath))
        {
            Console.Out.WriteLine($"Applied ids (Cosmos written): {result.AppliedIdsPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.ChangesPath))
        {
            Console.Out.WriteLine($"From→to change log: {result.ChangesPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.JournalPath))
        {
            Console.Out.WriteLine(
                "If rollback fails: repair from journal JSONL (applied=true → restore 'before'). See films-refactor/REPAIR.md");
        }
    }

    private static List<Guid> ParseIds(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Guid.Parse)
            .ToList();
}
