using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

/// <summary>
/// Turns a <see cref="CataloguePlayableFieldPatch"/> into an ETag-guarded Cosmos partial patch that touches only
/// <c>/services</c> and <c>/youTubeId</c>. Patch never creates a document, so a report or episode deleted or moved
/// mid-run returns 404 instead of being resurrected.
/// <para>
/// A null desired value is removed only when the read showed that member defined (an explicit JSON null counts).
/// Removing a path that is not defined is a Cosmos 400, and a typed model cannot tell an omitted member from null.
/// The value is never <c>Set</c> to null: a full save omits nulls. This patch is one <c>IfMatch</c> request, so a
/// 412 is an ETag conflict and there is no <c>IS_DEFINED</c> filter (#1154).
/// </para>
/// </summary>
public static class CataloguePlayableFieldPatchOperations
{
    public static IReadOnlyList<PatchOperation> Build(CataloguePlayableFieldPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var operations = new List<PatchOperation>(2);
        Add(patch.SetServices, "/services", patch.Services, patch.ServicesDefined);
        Add(patch.SetYouTubeId, "/youTubeId", patch.YouTubeId, patch.YouTubeIdDefined);
        return operations;

        void Add<TValue>(bool set, string path, TValue? value, bool defined) where TValue : class
        {
            if (!set)
            {
                return;
            }

            if (value is not null)
            {
                operations.Add(PatchOperation.Set(path, value));
                return;
            }

            if (defined)
            {
                operations.Add(PatchOperation.Remove(path));
            }
        }
    }

    internal static async Task<CataloguePlayablePatchOutcome> Apply(
        Container container,
        string id,
        PartitionKey partitionKey,
        string ifMatchETag,
        CataloguePlayableFieldPatch patch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchETag);
        if (patch.IsEmpty)
        {
            return CataloguePlayablePatchOutcome.Patched;
        }

        var operations = Build(patch);
        if (operations.Count == 0)
        {
            // Desired nulls whose members are already absent. An empty patch is a Cosmos 400.
            return CataloguePlayablePatchOutcome.Patched;
        }

        try
        {
            await container.PatchItemAsync<object>(
                id,
                partitionKey,
                operations,
                new PatchItemRequestOptions
                {
                    IfMatchEtag = ifMatchETag,
                    EnableContentResponseOnWrite = false
                });
            return CataloguePlayablePatchOutcome.Patched;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return CataloguePlayablePatchOutcome.NotFound;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            return CataloguePlayablePatchOutcome.Conflict;
        }
    }

    internal static async Task<ETagged<T>?> ReadWithETag<T>(Container container, string id, PartitionKey partitionKey)
    {
        try
        {
            // Typed deserialisation collapses an omitted member and an explicit JSON null. Presence is read from
            // the raw object: ContainsKey is true for a stored null (#1154).
            var response = await container.ReadItemAsync<JsonObject>(id, partitionKey);
            var raw = response.Resource
                      ?? throw new InvalidOperationException($"Document '{id}' was empty.");
            var options = container.Database.Client.ClientOptions.UseSystemTextJsonSerializerWithOptions;
            var item = raw.Deserialize<T>(options)
                       ?? throw new InvalidOperationException(
                           $"Document '{id}' did not deserialise to {typeof(T).Name}.");
            return new ETagged<T>(
                item,
                response.ETag,
                raw.ContainsKey("services"),
                raw.ContainsKey("youTubeId"));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
