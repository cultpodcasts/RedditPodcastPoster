using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryResultsRepository(
    IDataRepository repository,
    ILogger<DiscoveryResultsRepository> logger) : IDiscoveryResultsRepository
{
    public Task Save(DiscoveryResultsDocument discoveryResultsDocument)
    {
        logger.LogInformation($"{nameof(Save)} initiated.");
        return repository.Write(discoveryResultsDocument);
    }

    public IAsyncEnumerable<DiscoveryResultsDocument> GetAllUnprocessed()
    {
        return repository.GetAllBy<DiscoveryResultsDocument>(x => x.State == DiscoveryResultState.Unprocessed);
    }
}