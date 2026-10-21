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
