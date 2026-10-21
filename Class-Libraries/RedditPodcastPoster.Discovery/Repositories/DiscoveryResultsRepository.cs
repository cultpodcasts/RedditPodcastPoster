using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Discovery.Repositories;

public class DiscoveryResultsRepository(
    Container discoveryContainer,
    ILogger<DiscoveryResultsRepository> logger)
    : IDiscoveryResultsRepository
{
    public async Task Save(DiscoveryResultsDocument discoveryResultsDocument)
    {
        await discoveryContainer.UpsertItemAsync(discoveryResultsDocument,
            new PartitionKey(discoveryResultsDocument.Id.ToString()));
    }

    public async Task<int> Count()
    {
        var iterator = discoveryContainer.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));

        while (iterator.HasMoreResults)
        {
            try
            {
                foreach (var count in await iterator.ReadNextAsync())
                {
                    return count;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error counting discovery documents.", nameof(Count));
                throw;
            }
        }

        return 0;
    }

    public IAsyncEnumerable<DiscoveryResultsDocument> GetAll()
    {
        return GetByIdsInternal(x => true);
    }

    public IAsyncEnumerable<DiscoveryResultsDocument> GetAllUnprocessed()
    {
        return GetByIdsInternal(x => x.State == DiscoveryResultsDocumentState.Unprocessed);
    }

    public IAsyncEnumerable<DiscoveryResultsDocument> GetAllPendingSupervisor()
    {
        return GetByIdsInternal(x => x.State == DiscoveryResultsDocumentState.PendingSupervisor);
    }

    public async Task<bool> TryTransitionState(Guid documentId, DiscoveryResultsDocumentState from,
        DiscoveryResultsDocumentState to, CancellationToken cancellationToken = default)
    {
        ItemResponse<DiscoveryResultsDocument> read;
        try
        {
            read = await discoveryContainer.ReadItemAsync<DiscoveryResultsDocument>(documentId.ToString(),
                new PartitionKey(documentId.ToString()), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        var document = read.Resource;
        if (document.State != from)
        {
            return false;
        }

        document.State = to;
        try
        {
            await discoveryContainer.ReplaceItemAsync(document, documentId.ToString(),
                new PartitionKey(documentId.ToString()),
                new ItemRequestOptions { IfMatchEtag = read.ETag }, cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            logger.LogWarning("{Method}: '{DocumentId}' changed concurrently; not moved to {State}.",
                nameof(TryTransitionState), documentId, to);
            return false;
        }
    }

    public async Task SetProcessed(IEnumerable<Guid> ids)
    {
        // Keep documents (State=Processed) so MAX(discoveryBegan) remains available for dynamic lookback.
        // Matches Api MarkAsProcessed; do not delete.
        foreach (var id in ids.Distinct())
        {
            try
            {
                var document = await GetById(id);
                if (document is null)
                {
                    logger.LogWarning(
                        "{Method}: no {DocumentType} with id '{DocumentId}'.",
                        nameof(SetProcessed), nameof(DiscoveryResultsDocument), id);
                    continue;
                }

                if (document.State == DiscoveryResultsDocumentState.Processed)
                {
                    continue;
                }

                if (document.State == DiscoveryResultsDocumentState.PendingSupervisor)
                {
                    // Only a supervisor applying the held submission may promote a held report to Processed.
                    logger.LogWarning(
                        "{Method}: '{DocumentId}' is awaiting supervisor review; not marking processed.",
                        nameof(SetProcessed), id);
                    continue;
                }

                document.State = DiscoveryResultsDocumentState.Processed;
                await Save(document);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "{Method}: failure to mark {DocumentType} '{DocumentId}' as processed.",
                    nameof(SetProcessed), nameof(DiscoveryResultsDocument), id);
            }
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
        var idArray = ids.Distinct().ToArray();
        return GetByIdsInternal(x => Enumerable.Contains(idArray, x.Id));
    }

    /// <summary>
    /// Latest <see cref="DiscoveryResultsDocument.DiscoveryBegan"/> across all Discovery docs
    /// (unprocessed and processed). Curation must leave processed docs in Cosmos for lookback.
    /// </summary>
    public async Task<DateTime?> GetLatestDiscoveryBegan(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
                "SELECT VALUE MAX(c.discoveryBegan) FROM c WHERE c.type = @type")
            .WithParameter("@type", nameof(ModelType.Discovery));

        var iterator = discoveryContainer.GetItemQueryIterator<DateTime?>(query);
        while (iterator.HasMoreResults)
        {
            try
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                foreach (var value in response)
                {
                    return value;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error querying latest discoveryBegan.", nameof(GetLatestDiscoveryBegan));
                throw;
            }
        }

        return null;
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
