using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

/// <summary>
/// Cosmos patch that stores the news-organisation defaults (<c>indexing.bypassShortEpisodeChecking</c> and
/// <c>indexing.alwaysPromoteAsHero</c> = true) and touches nothing else. When the document has an <c>indexing</c>
/// object only the two members are set; when it has none, <c>/indexing</c> is added holding just the two flags (a
/// member cannot be set under a missing parent). Each patch is conditional on that shape, so a document whose
/// <c>indexing</c> was added or removed after it was read is not written.
/// </summary>
public static class NewsOrganisationDefaultsPatchOperations
{
    public const string BypassShortEpisodeCheckingPath = "/indexing/bypassShortEpisodeChecking";
    public const string AlwaysPromoteAsHeroPath = "/indexing/alwaysPromoteAsHero";
    public const string IndexingStoredFilter = "FROM c WHERE IS_OBJECT(c.indexing)";
    public const string IndexingMissingFilter = "FROM c WHERE NOT IS_DEFINED(c.indexing) OR IS_NULL(c.indexing)";

    public static (IReadOnlyList<PatchOperation> Operations, string FilterPredicate) Build(bool indexingStored)
    {
        if (indexingStored)
        {
            return (
                [
                    PatchOperation.Set(BypassShortEpisodeCheckingPath, true),
                    PatchOperation.Set(AlwaysPromoteAsHeroPath, true)
                ],
                IndexingStoredFilter);
        }

        var indexing = new JsonObject
        {
            ["bypassShortEpisodeChecking"] = true,
            ["alwaysPromoteAsHero"] = true
        };
        return ([PatchOperation.Set<JsonNode?>("/indexing", indexing)], IndexingMissingFilter);
    }

    internal static async Task<NewsOrganisationDefaultsPatchResult> Apply(Container container, Guid id, bool indexingStored)
    {
        var (operations, filter) = Build(indexingStored);
        try
        {
            await container.PatchItemAsync<JsonObject>(
                id.ToString(), new PartitionKey(id.ToString()), operations,
                new PatchItemRequestOptions { EnableContentResponseOnWrite = false, FilterPredicate = filter });
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            return NewsOrganisationDefaultsPatchResult.ShapeChanged;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NewsOrganisationDefaultsPatchResult.NotFound;
        }

        return NewsOrganisationDefaultsPatchResult.Patched;
    }
}
