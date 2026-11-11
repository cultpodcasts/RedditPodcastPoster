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
/// <para>
/// A member the pass cleared (it serialises to null or is omitted) is removed rather than set to an explicit JSON
/// null, so a patched document has the same shape as a full upsert (the container serialiser omits nulls, #1085).
/// Cosmos fails a Remove on a missing path, so each removal is its own single-operation patch guarded by
/// <see cref="FilterPredicateFor"/> (<c>IS_DEFINED</c>); a failed precondition there means it is already absent.
/// </para>
/// <para>
/// Order matters because the chunks are separate writes: removals go first and <c>/lastIndexed</c> (the pass's
/// success marker) is the very last operation, so a patch that fails part-way never records a completed pass.
/// </para>
/// </summary>
public static class CatalogueParentIndexerPatchOperations
{
    public const int MaxOperationsPerPatch = 10;
    private const string IndexingPathPrefix = "/indexing/";

    public static IReadOnlyList<IReadOnlyList<PatchOperation>> Build(
        CatalogueParentIndexerPatch patch,
        JsonSerializerOptions? serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var operations = new List<PatchOperation>();
        var removals = new List<PatchOperation>();
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
                var path = $"{IndexingPathPrefix}{name}";
                if (indexing.TryGetPropertyValue(name, out var value) && value is not null)
                {
                    operations.Add(PatchOperation.Set<JsonNode?>(path, value.DeepClone()));
                }
                else
                {
                    removals.Add(PatchOperation.Remove(path));
                }
            }
        }

        if (patch.LastIndexed is { } lastIndexed)
        {
            // Last, so it is only written once every other change of this pass has been applied.
            operations.Add(PatchOperation.Set("/lastIndexed", lastIndexed));
        }

        return removals.Select(removal => (IReadOnlyList<PatchOperation>) [removal])
            .Concat(operations.Chunk(MaxOperationsPerPatch).Select(chunk => (IReadOnlyList<PatchOperation>) chunk))
            .ToList();
    }

    /// <summary>
    /// The patch filter a chunk from <see cref="Build"/> must be sent with: a single <c>/indexing/{member}</c>
    /// removal only applies when the stored member is defined (an explicit null counts as defined, so a stored null
    /// is removed too), because Cosmos fails a Remove on a missing path. Other chunks are unfiltered (null).
    /// </summary>
    public static string? FilterPredicateFor(IReadOnlyList<PatchOperation> chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk is not [{ OperationType: PatchOperationType.Remove, Path: var path }] ||
            !path.StartsWith(IndexingPathPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var member = path[IndexingPathPrefix.Length..].Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"FROM c WHERE IS_DEFINED(c.indexing[\"{member}\"])";
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
                var filter = FilterPredicateFor(chunk);
                try
                {
                    await container.PatchItemAsync<JsonObject>(
                        id.ToString(), partitionKey, chunk,
                        new PatchItemRequestOptions { EnableContentResponseOnWrite = false, FilterPredicate = filter });
                }
                catch (CosmosException ex) when (filter != null &&
                                                 ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
                {
                    // The member is already absent, which is what the removal wanted.
                }
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        return true;
    }
}
