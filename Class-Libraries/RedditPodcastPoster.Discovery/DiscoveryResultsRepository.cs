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

    public IAsyncEnumerable<DiscoveryResultsDocument> GetAllUnprocessed()
    {
        return repository.GetAllBy<DiscoveryResultsDocument>(x => x.State == DiscoveryResultsDocumentState.Unprocessed);
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
                "{SetProcessedName} Failure to delete {DiscoveryResultsDocumentName}s with ids: {Join}.", nameof(SetProcessed), nameof(DiscoveryResultsDocument), string.Join(", ", ids));
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