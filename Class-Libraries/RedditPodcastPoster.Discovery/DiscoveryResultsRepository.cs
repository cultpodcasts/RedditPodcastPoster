using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
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
        return repository.GetAll<DiscoveryResultsDocument>().Where(x => x.State == DiscoveryResultState.Unprocessed);
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
}