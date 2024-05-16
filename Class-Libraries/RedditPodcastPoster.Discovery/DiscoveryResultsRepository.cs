using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryResultsRepository(
    IDataRepository repository,
    ILogger<DiscoveryResultsRepository> logger) : IDiscoveryResultsRepository
{
    public Task Save(DiscoveryResultsDocument discoveryResultsDocument)
    {
        return repository.Write(discoveryResultsDocument);
    }
}