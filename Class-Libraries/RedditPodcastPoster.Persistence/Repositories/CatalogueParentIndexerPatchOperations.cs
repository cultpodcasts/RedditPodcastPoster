using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

/// <summary>
/// Turns a <see cref="CatalogueParentIndexerPatch"/> into Cosmos patch operations that touch only
/// <c>/lastIndexed</c>, <c>/latestReleased</c> and the changed <c>/indexing/{member}</c> paths.
/// Values are serialised with the container's own options, so each patched member has the same wire
/// shape a full upsert would write (enum strings, TimeSpan format). Cosmos allows at most
/// <see cref="MaxOperationsPerPatch"/> operations per patch, so the result is chunked.
/// </summary>
public static class CatalogueParentIndexerPatchOperations
{
    public const int MaxOperationsPerPatch = 10;

    public static IReadOnlyList<IReadOnlyList<PatchOperation>> Build(
        CatalogueParentIndexerPatch patch,
        JsonSerializerOptions? serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var operations = new List<PatchOperation>();
        if (patch.LastIndexed is { } lastIndexed)
        {
            operations.Add(PatchOperation.Set("/lastIndexed", lastIndexed));
        }

        if (patch.LatestReleased is { } latestReleased)
        {
            operations.Add(PatchOperation.Set("/latestReleased", latestReleased));
        }

        if (patch.ChangedIndexingProperties.Count > 0)
        {
            var indexing = JsonSerializer.SerializeToNode(patch.Indexing, serializerOptions) as JsonObject
                           ?? throw new InvalidOperationException("Indexing settings did not serialise to a JSON object.");
            foreach (var name in patch.ChangedIndexingProperties.Distinct().Order(StringComparer.Ordinal))
            {
                indexing.TryGetPropertyValue(name, out var value);
                operations.Add(PatchOperation.Set<JsonNode?>($"/indexing/{name}", value?.DeepClone()));
            }
        }

        return operations.Chunk(MaxOperationsPerPatch).Select(chunk => (IReadOnlyList<PatchOperation>) chunk).ToList();
    }

    /// <summary>
    /// Cosmos patch filter that only lets a <c>/latestReleased</c> raise through when the stored value is missing,
    /// null or older, so a concurrent writer that already raised it is never lowered again. The value is written with
    /// the container's own serialiser options, so it compares against the stored wire shape.
    /// </summary>
    public static string LatestReleasedOlderThanFilter(DateTime latestReleased, JsonSerializerOptions? serializerOptions)
    {
        var wire = JsonSerializer.SerializeToElement(latestReleased, serializerOptions).GetString()
                   ?? throw new InvalidOperationException("latestReleased did not serialise to a JSON string.");
        return "FROM c WHERE NOT IS_DEFINED(c.latestReleased) OR IS_NULL(c.latestReleased) " +
               $"OR c.latestReleased < '{wire.Replace("'", "\\'")}'";
    }

    /// <summary>
    /// Sets <c>/latestReleased</c> only when the stored value is missing or older (one conditional patch). A failed
    /// condition (412) means it is already as new or newer, which counts as done. Returns false when the parent no
    /// longer exists.
    /// </summary>
    internal static async Task<bool> RaiseLatestReleased(Container container, Guid id, DateTime latestReleased)
    {
        var options = container.Database.Client.ClientOptions.UseSystemTextJsonSerializerWithOptions;
        try
        {
            await container.PatchItemAsync<JsonObject>(
                id.ToString(), new PartitionKey(id.ToString()),
                [PatchOperation.Set("/latestReleased", latestReleased)],
                new PatchItemRequestOptions
                {
                    EnableContentResponseOnWrite = false,
                    FilterPredicate = LatestReleasedOlderThanFilter(latestReleased, options)
                });
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        return true;
    }

    /// <summary>Applies the patch. Returns false when the parent no longer exists (deleted mid-pass).</summary>
    internal static async Task<bool> Apply(Container container, Guid id, CatalogueParentIndexerPatch patch)
    {
        if (patch.IsEmpty)
        {
            return true;
        }

        var options = container.Database.Client.ClientOptions.UseSystemTextJsonSerializerWithOptions;
        var partitionKey = new PartitionKey(id.ToString());
        try
        {
            foreach (var chunk in Build(patch, options))
            {
                await container.PatchItemAsync<JsonObject>(
                    id.ToString(), partitionKey, chunk,
                    new PatchItemRequestOptions { EnableContentResponseOnWrite = false });
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        return true;
    }
}
