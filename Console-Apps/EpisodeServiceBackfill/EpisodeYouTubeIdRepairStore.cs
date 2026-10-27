using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Factories;

namespace EpisodeServiceBackfill;

/// <summary>Result of one guarded <c>ids.youtube</c> patch.</summary>
public enum YouTubeIdPatchOutcome
{
    Patched,

    /// <summary>412: the per-plan condition no longer holds (id set, ids object created, or url changed since the scan).</summary>
    ConditionNotMet,

    /// <summary>404: the document was deleted since the scan.</summary>
    NotFound,

    /// <summary>
    /// 400: Cosmos rejects a set of <c>/ids/youtube</c> when the parent <c>ids</c> object was removed after the
    /// scan. Nothing was written; re-run to re-plan the document with the <c>/ids</c> path.
    /// </summary>
    BadRequest,

    Failed
}

/// <summary>Cosmos access for <c>--youtube-id-only</c>, kept behind an interface so the runner is testable.</summary>
public interface IEpisodeYouTubeIdRepairStore
{
    IAsyncEnumerable<string> QueryCandidates(IReadOnlyList<Guid> ids);

    IAsyncEnumerable<string> QueryStoredIdsWithUrl(IReadOnlyList<Guid> ids);

    Task<YouTubeIdPatchOutcome> Patch(EpisodeYouTubeIdRepairPlan plan);
}

public class CosmosEpisodeYouTubeIdRepairStore(ICosmosDbContainerFactory containerFactory)
    : IEpisodeYouTubeIdRepairStore
{
    private readonly Lazy<Container> _container = new(containerFactory.CreateEpisodesContainer);

    public IAsyncEnumerable<string> QueryCandidates(IReadOnlyList<Guid> ids) =>
        Query(BuildQuery(EpisodeYouTubeIdRepair.CandidateQuery, ids));

    public IAsyncEnumerable<string> QueryStoredIdsWithUrl(IReadOnlyList<Guid> ids) =>
        Query(BuildQuery(EpisodeYouTubeIdRepair.StoredIdWithUrlQuery, ids));

    public async Task<YouTubeIdPatchOutcome> Patch(EpisodeYouTubeIdRepairPlan plan)
    {
        try
        {
            await _container.Value.PatchItemAsync<JsonElement>(
                plan.EpisodeId.ToString(),
                new PartitionKey(plan.PodcastId.ToString()),
                [PatchOperation.Set(plan.PatchPath, EpisodeYouTubeIdRepair.PatchValue(plan))],
                new PatchItemRequestOptions
                {
                    FilterPredicate = EpisodeYouTubeIdRepair.PatchFilterPredicateFor(plan),
                    EnableContentResponseOnWrite = false
                });
            return YouTubeIdPatchOutcome.Patched;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            return YouTubeIdPatchOutcome.ConditionNotMet;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return YouTubeIdPatchOutcome.NotFound;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return YouTubeIdPatchOutcome.BadRequest;
        }
        catch (CosmosException)
        {
            return YouTubeIdPatchOutcome.Failed;
        }
    }

    /// <summary>Appends a parameterised <c>c.id IN (...)</c> filter when ids are given.</summary>
    public static QueryDefinition BuildQuery(string baseQuery, IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new QueryDefinition(baseQuery);
        }

        var names = ids.Select((_, i) => $"@id{i}").ToList();
        var query = new QueryDefinition(baseQuery + $" AND c.id IN ({string.Join(", ", names)})");
        for (var i = 0; i < ids.Count; i++)
        {
            query = query.WithParameter(names[i], ids[i].ToString());
        }

        return query;
    }

    private async IAsyncEnumerable<string> Query(QueryDefinition query)
    {
        using var iterator = _container.Value.GetItemQueryStreamIterator(query);
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
