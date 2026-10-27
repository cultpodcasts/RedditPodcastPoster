using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Factories;

namespace EpisodeServiceBackfill;

/// <summary>
/// <c>--youtube-id-only</c> runner. Dry-run unless <c>--apply</c>. Each write is one Cosmos patch of
/// <c>/ids/youtube</c> (or <c>/ids</c> when absent) guarded by a filter predicate, so nothing else in the
/// document changes and re-running is a no-op. The patch bumps <c>_ts</c>, which is the high-water mark
/// the podcast-episode Azure Search indexer reads, so its next run re-indexes the episode with the id.
/// </summary>
public class EpisodeYouTubeIdRepairRunner(
    ICosmosDbContainerFactory containerFactory,
    ILogger<EpisodeYouTubeIdRepairRunner> logger)
{
    public async Task<int> Run(EpisodeServiceBackfillRequest request)
    {
        var container = containerFactory.CreateEpisodesContainer();
        var requestedIds = ParseIds(request.Ids);
        var plans = new List<EpisodeYouTubeIdRepairPlan>();
        var skipped = new Dictionary<EpisodeYouTubeIdRepair.SkipReason, int>();

        await foreach (var json in Query(container, BuildQuery(requestedIds)))
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
            var missing = requestedIds.Except(plans.Select(x => x.EpisodeId)).ToList();
            foreach (var id in missing)
            {
                Console.WriteLine($"  {id}: not a candidate (not found, id already set, or url has no video id).");
            }
        }

        if (!request.Apply)
        {
            Console.WriteLine("Dry-run only. Re-run with --apply to patch ids.youtube.");
            return 0;
        }

        int patched = 0, alreadySet = 0, notFound = 0, failed = 0;
        foreach (var plan in plans)
        {
            try
            {
                await container.PatchItemAsync<JsonElement>(
                    plan.EpisodeId.ToString(),
                    new PartitionKey(plan.PodcastId.ToString()),
                    [PatchOperation.Set(plan.PatchPath, EpisodeYouTubeIdRepair.PatchValue(plan))],
                    new PatchItemRequestOptions
                    {
                        FilterPredicate = EpisodeYouTubeIdRepair.PatchFilterPredicate,
                        EnableContentResponseOnWrite = false
                    });
                patched++;
                Console.WriteLine($"  patched {plan.EpisodeId} ids.youtube={plan.YouTubeId}");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                alreadySet++;
                Console.WriteLine($"  skipped {plan.EpisodeId}: ids.youtube was set since the scan.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                notFound++;
                Console.WriteLine($"  skipped {plan.EpisodeId}: not found.");
            }
            catch (CosmosException ex)
            {
                failed++;
                logger.LogError(ex, "Patch failed for episode {EpisodeId} ({StatusCode}).", plan.EpisodeId, ex.StatusCode);
            }
        }

        Console.WriteLine(
            $"Applied: patched={patched} already-set={alreadySet} not-found={notFound} failed={failed}. " +
            "The podcast-episode search indexer picks these up on its next run (_ts changed).");
        return failed > 0 ? 2 : 0;
    }

    internal static QueryDefinition BuildQuery(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new QueryDefinition(EpisodeYouTubeIdRepair.CandidateQuery);
        }

        var names = ids.Select((_, i) => $"@id{i}").ToList();
        var query = new QueryDefinition(
            EpisodeYouTubeIdRepair.CandidateQuery + $" AND c.id IN ({string.Join(", ", names)})");
        for (var i = 0; i < ids.Count; i++)
        {
            query = query.WithParameter(names[i], ids[i].ToString());
        }

        return query;
    }

    private static List<Guid> ParseIds(string? ids) =>
        string.IsNullOrWhiteSpace(ids)
            ? []
            : ids.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Guid.Parse)
                .Distinct()
                .ToList();

    private static async IAsyncEnumerable<string> Query(Container container, QueryDefinition query)
    {
        using var iterator = container.GetItemQueryStreamIterator(query);
        while (iterator.HasMoreResults)
        {
            using var response = await iterator.ReadNextAsync();
            response.EnsureSuccessStatusCode();
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
}
