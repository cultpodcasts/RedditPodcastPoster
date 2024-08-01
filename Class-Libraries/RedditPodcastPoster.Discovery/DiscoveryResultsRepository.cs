using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryResultsRepository(
    IDataRepository repository,
    Container container,
    ILogger<DiscoveryResultsRepository> logger) : IDiscoveryResultsRepository
{
    public Task Save(DiscoveryResultsDocument discoveryResultsDocument)
    {
        logger.LogInformation($"{nameof(Save)} initiated.");
        return repository.Write(discoveryResultsDocument);
    }



    public async IAsyncEnumerable<DiscoveryResultsDocument> GetAllUnprocessed()
    {
        var unprocessedDocumentIds = new List<Guid>();
        var unprocessedDiscoveryResultDocuments = new QueryDefinition(
            @"
                SELECT c.id FROM c WHERE c.type = 'Discovery' AND c.state='Unprocessed'
            ");

        using var feed = container.GetItemQueryIterator<IdRecord>(unprocessedDiscoveryResultDocuments);
        while (feed.HasMoreResults)
        {
            var readNextAsync = await feed.ReadNextAsync();
            unprocessedDocumentIds.AddRange(readNextAsync.Select(x=>x.Id));
        }

        foreach (var unprocessedDocumentId in unprocessedDocumentIds)
        {
            yield return await GetById(unprocessedDocumentId);
        }

    }

    public async Task SetProcessed(IEnumerable<Guid> ids)
    {
        var deleteTasks = ids.Select(id =>
            container.DeleteItemAsync<DiscoveryResultsDocument>(id.ToString(),
                new PartitionKey(CosmosSelectorExtensions.GetModelType<DiscoveryResultsDocument>().ToString())));
        try
        {
            var delete = await Task.WhenAll(deleteTasks);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"{nameof(SetProcessed)} Failure to delete {nameof(DiscoveryResultsDocument)}s with ids: {string.Join(", ", ids)}.");
        }
    }

    public Task<DiscoveryResultsDocument?> GetById(Guid documentId)
    {
        return repository.GetBy<DiscoveryResultsDocument>(x => x.Id == documentId);
    }

    public IAsyncEnumerable<DiscoveryResultsDocument> GetByIds(IEnumerable<Guid> ids)
    {
        return repository.GetAllBy<DiscoveryResultsDocument>(x => ids.Contains(x.Id));
    }
}


public class IdRecord
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
}