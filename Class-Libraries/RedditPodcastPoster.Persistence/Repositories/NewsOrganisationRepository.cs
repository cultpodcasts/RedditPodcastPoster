using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

public class NewsOrganisationRepository(
    Container newsOrganisationsContainer,
    ILogger<NewsOrganisationRepository> logger,
    IFileKeyClaimStore fileKeyClaims)
    : INewsOrganisationRepository
{
    public async Task<NewsOrganisation?> GetNewsOrganisation(Guid newsOrganisationId)
    {
        try
        {
            return await newsOrganisationsContainer.ReadItemAsync<NewsOrganisation>(
                newsOrganisationId.ToString(),
                new PartitionKey(newsOrganisationId.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task Save(NewsOrganisation organisation)
    {
        await newsOrganisationsContainer.UpsertItemAsync(
            organisation,
            new PartitionKey(organisation.Id.ToString()));
        await fileKeyClaims.Acknowledge(organisation.FileKey);
    }

    public Task<bool> PatchIndexerState(Guid newsOrganisationId, CatalogueParentIndexerPatch patch) =>
        CatalogueParentIndexerPatchOperations.Apply(newsOrganisationsContainer, newsOrganisationId, patch);

    public Task<NewsOrganisationDefaultsPatchResult> PatchCreationDefaults(Guid newsOrganisationId, bool indexingStored) =>
        NewsOrganisationDefaultsPatchOperations.Apply(newsOrganisationsContainer, newsOrganisationId, indexingStored);

    public async Task Delete(Guid newsOrganisationId)
    {
        try
        {
            await newsOrganisationsContainer.DeleteItemAsync<NewsOrganisation>(
                newsOrganisationId.ToString(),
                new PartitionKey(newsOrganisationId.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // idempotent delete
        }
    }

    public Task<int> Count()
    {
        var iterator = newsOrganisationsContainer.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));

        return CatalogueFeed.Count(
            iterator,
            ex => logger.LogError(ex, "{method}: error counting news organisations.", nameof(Count)));
    }

    public IAsyncEnumerable<NewsOrganisation> GetAll()
    {
        var query = newsOrganisationsContainer
            .GetItemLinqQueryable<NewsOrganisation>(requestOptions: new QueryRequestOptions())
            .AsQueryable();

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving news organisations.", nameof(GetAll)));
    }

    public async Task<NewsOrganisation?> GetBy(Expression<Func<NewsOrganisation, bool>> selector)
    {
        var query = newsOrganisationsContainer
            .GetItemLinqQueryable<NewsOrganisation>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                return item;
            }
        }

        return null;
    }

    public IAsyncEnumerable<NewsOrganisation> GetAllBy(Expression<Func<NewsOrganisation, bool>> selector)
    {
        var query = newsOrganisationsContainer
            .GetItemLinqQueryable<NewsOrganisation>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving news organisations with selector.", nameof(GetAllBy)));
    }

    public IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<NewsOrganisation, bool>> selector,
        Expression<Func<NewsOrganisation, TProjection>> projection)
    {
        var query = newsOrganisationsContainer
            .GetItemLinqQueryable<NewsOrganisation>(requestOptions: new QueryRequestOptions())
            .Where(selector)
            .Select(projection);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving projected news organisations.", nameof(GetAllBy)));
    }
}
