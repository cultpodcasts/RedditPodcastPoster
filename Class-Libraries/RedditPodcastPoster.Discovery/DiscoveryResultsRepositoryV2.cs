using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryResultsRepositoryV2(
    Container discoveryContainer,
    ILogger<DiscoveryResultsRepositoryV2> logger)
    : IDiscoveryResultsRepositoryV2
{
    public async Task Save(DiscoveryResultsDocument discoveryResultsDocument)
    {
        await discoveryContainer.UpsertItemAsync(discoveryResultsDocument,
            new PartitionKey(discoveryResultsDocument.Id.ToString()));
    }

    public IAsyncEnumerable<DiscoveryResultsDocument> GetAll()
    {
        return GetByIdsInternal(x => true);
    }

    public IAsyncEnumerable<DiscoveryResultsDocument> GetAllUnprocessed()
    {
        return GetByIdsInternal(x => x.State == DiscoveryResultsDocumentState.Unprocessed);
    }

    public async Task SetProcessed(IEnumerable<Guid> ids)
    {
        var deleteTasks = ids.Select(id =>
            discoveryContainer.DeleteItemAsync<DiscoveryResultsDocument>(id.ToString(), new PartitionKey(id.ToString())));

        try
        {
            await Task.WhenAll(deleteTasks);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{SetProcessedName} failure to delete {DiscoveryResultsDocumentName}s with ids: {Join}.",
                nameof(SetProcessed), nameof(DiscoveryResultsDocument), string.Join(", ", ids));
        }
    }

    public async Task<DiscoveryResultsDocument?> GetById(Guid documentId)
    {
        try
        {
            return await discoveryContainer.ReadItemAsync<DiscoveryResultsDocument>(documentId.ToString(),
                new PartitionKey(documentId.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public IAsyncEnumerable<DiscoveryResultsDocument> GetByIds(IEnumerable<Guid> ids)
    {
        var idSet = ids.ToHashSet();
        return GetByIdsInternal(x => idSet.Contains(x.Id));
    }

    private async IAsyncEnumerable<DiscoveryResultsDocument> GetByIdsInternal(
        System.Linq.Expressions.Expression<Func<DiscoveryResultsDocument, bool>> predicate)
    {
        var query = discoveryContainer
            .GetItemLinqQueryable<DiscoveryResultsDocument>(requestOptions: new QueryRequestOptions())
            .Where(predicate);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<DiscoveryResultsDocument> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving discovery documents.", nameof(GetByIdsInternal));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
