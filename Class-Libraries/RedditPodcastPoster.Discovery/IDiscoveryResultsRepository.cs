using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryResultsRepository
{
    Task Save(DiscoveryResultsDocument discoveryResultsDocument);
}

public class DiscoveryResultsRepository(
    IDataRepository repository,
    ILogger<DiscoveryResultsRepository> logger) : IDiscoveryResultsRepository
{
    public Task Save(DiscoveryResultsDocument discoveryResultsDocument)
    {
            return repository.Write(discoveryResultsDocument);
    }
}