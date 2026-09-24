using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;

namespace EpisodeWireRewrite;

public interface IWireCutoverEpisodeStore
{
    /// <summary>
    /// Stable document order for repeatable limited runs: ORDER BY c.id ascending.
    /// </summary>
    public const string OrderedAllQuery = "SELECT * FROM c ORDER BY c.id ASC";

    IAsyncEnumerable<string> QueryAllRawAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> QueryByEpisodeIdsAsync(
        IReadOnlyList<Guid> episodeIds,
        CancellationToken cancellationToken = default);

    Task<string?> GetRawAsync(Guid podcastId, Guid episodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Surgical cutover-field update: patches only paths that differ from live JSON.
    /// </summary>
    Task<bool> ApplyCutoverStateAsync(
        Guid podcastId,
        Guid episodeId,
        IReadOnlyList<WireFieldState> desired,
        CancellationToken cancellationToken = default);
}

public sealed class CosmosWireCutoverEpisodeStore(
    Container container,
    ILogger<CosmosWireCutoverEpisodeStore> logger) : IWireCutoverEpisodeStore
{
    public async IAsyncEnumerable<string> QueryAllRawAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(IWireCutoverEpisodeStore.OrderedAllQuery);
        using var iterator = container.GetItemQueryIterator<JsonElement>(query);
        while (iterator.HasMoreResults)
        {
            FeedResponse<JsonElement> page;
            try
            {
                page = await iterator.ReadNextAsync(cancellationToken);
            }
            catch (CosmosException ex)
            {
                logger.LogError(ex, "Wire cutover: Cosmos query failed.");
                throw;
            }

            foreach (var el in page)
            {
                yield return el.GetRawText();
            }
        }
    }

    public async IAsyncEnumerable<string> QueryByEpisodeIdsAsync(
        IReadOnlyList<Guid> episodeIds,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(episodeIds);
        foreach (var id in episodeIds.OrderBy(x => x))
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id.ToString());
            using var iterator = container.GetItemQueryIterator<JsonElement>(query);
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                foreach (var el in page)
                {
                    yield return el.GetRawText();
                }
            }
        }
    }

    public async Task<string?> GetRawAsync(
        Guid podcastId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.ReadItemAsync<JsonElement>(
                episodeId.ToString(),
                new PartitionKey(podcastId.ToString()),
                cancellationToken: cancellationToken);
            return response.Resource.GetRawText();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ApplyCutoverStateAsync(
        Guid podcastId,
        Guid episodeId,
        IReadOnlyList<WireFieldState> desired,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desired);
        var live = await GetRawAsync(podcastId, episodeId, cancellationToken);
        if (live is null)
        {
            logger.LogWarning(
                "Wire cutover: episode {EpisodeId} podcast {PodcastId} not found.",
                episodeId,
                podcastId);
            return false;
        }

        using var doc = JsonDocument.Parse(live);
        var current = WireCutoverPlanner.Capture(doc.RootElement);
        var ops = WireCutoverPatchBuilder.BuildDelta(current, desired);
        if (ops.Count == 0)
        {
            return true;
        }

        try
        {
            await container.PatchItemAsync<Episode>(
                episodeId.ToString(),
                new PartitionKey(podcastId.ToString()),
                ops.ToList(),
                cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
