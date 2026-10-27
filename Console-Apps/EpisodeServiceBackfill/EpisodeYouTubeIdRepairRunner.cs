using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EpisodeServiceBackfill;

/// <summary>What one <c>--youtube-id-only</c> run planned, reported and wrote.</summary>
public sealed record EpisodeYouTubeIdRepairResult(
    int ExitCode,
    IReadOnlyList<EpisodeYouTubeIdRepairPlan> Plans,
    IReadOnlyList<EpisodeYouTubeIdMismatch> Mismatches,
    IReadOnlyDictionary<YouTubeIdPatchOutcome, int> Outcomes);

/// <summary>
/// <c>--youtube-id-only</c> runner. Dry-run unless <c>--apply</c>. Each write is one Cosmos patch of
/// <c>/ids/youtube</c> (or <c>/ids</c> when absent) guarded by a per-plan filter predicate, so nothing else in
/// the document changes and re-running is a no-op. The patch bumps <c>_ts</c>, which is the high-water mark
/// the podcast-episode Azure Search indexer reads, so its next run re-indexes the episode with the id.
/// The dry run also lists stored ids that disagree with the url; policy is to keep the stored id, so those
/// are reported and never patched.
/// </summary>
public class EpisodeYouTubeIdRepairRunner(
    IEpisodeYouTubeIdRepairStore store,
    ILogger<EpisodeYouTubeIdRepairRunner> logger)
{
    public const int InvalidArgumentsExitCode = 1;
    public const int PatchFailuresExitCode = 2;

    public async Task<int> Run(EpisodeServiceBackfillRequest request) => (await Execute(request)).ExitCode;

    public async Task<EpisodeYouTubeIdRepairResult> Execute(EpisodeServiceBackfillRequest request)
    {
        var outcomes = new Dictionary<YouTubeIdPatchOutcome, int>();
        if (!TryParseIds(request.Ids, out var requestedIds, out var invalidIds))
        {
            Console.Error.WriteLine(
                $"--ids must be a comma, semicolon or space separated list of episode GUIDs. Not a GUID: {string.Join(", ", invalidIds.Select(x => $"'{x}'"))}.");
            return new EpisodeYouTubeIdRepairResult(InvalidArgumentsExitCode, [], [], outcomes);
        }

        var plans = new List<EpisodeYouTubeIdRepairPlan>();
        var skipped = new Dictionary<EpisodeYouTubeIdRepair.SkipReason, int>();
        await foreach (var json in store.QueryCandidates(requestedIds))
        {
            var plan = EpisodeYouTubeIdRepair.Plan(json, out var reason);
            if (plan is null)
            {
                skipped[reason] = skipped.GetValueOrDefault(reason) + 1;
                continue;
            }

            plans.Add(plan);
        }

        plans = plans.OrderBy(x => x.Ts).ThenBy(x => x.EpisodeId).ToList();
        if (request.Limit > 0 && requestedIds.Count == 0 && plans.Count > request.Limit && !request.All)
        {
            Console.WriteLine(
                $"{plans.Count} candidate(s); --limit {request.Limit} keeps the oldest {request.Limit}. Pass --all for every candidate.");
            plans = plans.Take(request.Limit).ToList();
        }

        Console.WriteLine($"youtube-id-only: {plans.Count} episode(s) with services.youtube.url and no ids.youtube.");
        foreach (var plan in plans)
        {
            Console.WriteLine(
                $"  {plan.EpisodeId} podcast={plan.PodcastId} _ts={DateTimeOffset.FromUnixTimeSeconds(plan.Ts):u} " +
                $"removed={plan.Removed} set {plan.PatchPath} = {JsonSerializer.Serialize(EpisodeYouTubeIdRepair.PatchValue(plan))} (from {plan.Url})");
        }

        foreach (var pair in skipped.Where(x => x.Key != EpisodeYouTubeIdRepair.SkipReason.None))
        {
            Console.WriteLine($"  skipped {pair.Key}: {pair.Value}");
        }

        if (requestedIds.Count > 0)
        {
            foreach (var id in requestedIds.Except(plans.Select(x => x.EpisodeId)))
            {
                Console.WriteLine($"  {id}: not a candidate (not found, id already set, or url has no video id).");
            }
        }

        if (!request.Apply)
        {
            var mismatches = await ReportMismatches(requestedIds);
            Console.WriteLine("Dry-run only. Re-run with --apply to patch ids.youtube.");
            return new EpisodeYouTubeIdRepairResult(0, plans, mismatches, outcomes);
        }

        foreach (var plan in plans)
        {
            var outcome = await store.Patch(plan);
            outcomes[outcome] = outcomes.GetValueOrDefault(outcome) + 1;
            switch (outcome)
            {
                case YouTubeIdPatchOutcome.Patched:
                    Console.WriteLine($"  patched {plan.EpisodeId} ids.youtube={plan.YouTubeId}");
                    break;
                case YouTubeIdPatchOutcome.ConditionNotMet:
                    Console.WriteLine(
                        $"  skipped {plan.EpisodeId}: changed since the scan (id or ids object set, or url changed).");
                    break;
                case YouTubeIdPatchOutcome.NotFound:
                    Console.WriteLine($"  skipped {plan.EpisodeId}: not found.");
                    break;
                case YouTubeIdPatchOutcome.BadRequest:
                    Console.WriteLine(
                        $"  skipped {plan.EpisodeId}: rejected (ids object removed since the scan?). Nothing written; re-run to re-plan.");
                    break;
                default:
                    logger.LogError("Patch failed for episode {EpisodeId}.", plan.EpisodeId);
                    break;
            }
        }

        Console.WriteLine(
            $"Applied: patched={Count(YouTubeIdPatchOutcome.Patched)} changed-since-scan={Count(YouTubeIdPatchOutcome.ConditionNotMet)} " +
            $"not-found={Count(YouTubeIdPatchOutcome.NotFound)} bad-request={Count(YouTubeIdPatchOutcome.BadRequest)} " +
            $"failed={Count(YouTubeIdPatchOutcome.Failed)}. " +
            "The podcast-episode search indexer picks these up on its next run (_ts changed).");
        var exitCode = Count(YouTubeIdPatchOutcome.Failed) + Count(YouTubeIdPatchOutcome.BadRequest) > 0
            ? PatchFailuresExitCode
            : 0;
        return new EpisodeYouTubeIdRepairResult(exitCode, plans, [], outcomes);

        int Count(YouTubeIdPatchOutcome outcome) => outcomes.GetValueOrDefault(outcome);
    }

    private async Task<IReadOnlyList<EpisodeYouTubeIdMismatch>> ReportMismatches(IReadOnlyList<Guid> requestedIds)
    {
        var mismatches = new List<EpisodeYouTubeIdMismatch>();
        await foreach (var json in store.QueryStoredIdsWithUrl(requestedIds))
        {
            if (EpisodeYouTubeIdRepair.FindMismatch(json) is { } mismatch)
            {
                mismatches.Add(mismatch);
            }
        }

        mismatches = mismatches.OrderBy(x => x.EpisodeId).ToList();
        Console.WriteLine(
            $"id/url mismatches: {mismatches.Count} episode(s) whose ids.youtube is not the url's video. " +
            "Reported only; the stored id is kept (docs/episode-services.md).");
        foreach (var mismatch in mismatches)
        {
            Console.WriteLine(
                $"  {mismatch.EpisodeId} podcast={mismatch.PodcastId} ids.youtube={mismatch.StoredYouTubeId} " +
                $"url-id={mismatch.UrlYouTubeId} (url {mismatch.Url})");
        }

        return mismatches;
    }

    /// <summary>Parses <c>--ids</c>; false with the offending tokens when any is not a GUID.</summary>
    public static bool TryParseIds(string? ids, out IReadOnlyList<Guid> parsed, out IReadOnlyList<string> invalid)
    {
        var valid = new List<Guid>();
        var bad = new List<string>();
        if (!string.IsNullOrWhiteSpace(ids))
        {
            foreach (var token in ids.Split([',', ';', ' '],
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(token, out var id))
                {
                    valid.Add(id);
                }
                else
                {
                    bad.Add(token);
                }
            }
        }

        parsed = valid.Distinct().ToList();
        invalid = bad;
        return bad.Count == 0;
    }
}
