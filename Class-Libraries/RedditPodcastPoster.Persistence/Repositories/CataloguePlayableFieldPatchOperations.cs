using System.Net;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

/// <summary>
/// Turns a <see cref="CataloguePlayableFieldPatch"/> into an ETag-guarded Cosmos partial patch that touches only
/// <c>/services</c> and <c>/youTubeId</c>. Patch never creates a document, so a report or episode deleted or moved
/// mid-run returns 404 instead of being resurrected.
/// </summary>
public static class CataloguePlayableFieldPatchOperations
{
    public static IReadOnlyList<PatchOperation> Build(CataloguePlayableFieldPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var operations = new List<PatchOperation>(2);
        if (patch.SetServices)
        {
            operations.Add(patch.Services is null
                ? PatchOperation.Set<object?>("/services", null)
                : PatchOperation.Set("/services", patch.Services));
        }

        if (patch.SetYouTubeId)
        {
            operations.Add(PatchOperation.Set<string?>("/youTubeId", patch.YouTubeId));
        }

        return operations;
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

        try
        {
            await container.PatchItemAsync<object>(
                id,
                partitionKey,
                Build(patch),
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
            var response = await container.ReadItemAsync<T>(id, partitionKey);
            return new ETagged<T>(response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
