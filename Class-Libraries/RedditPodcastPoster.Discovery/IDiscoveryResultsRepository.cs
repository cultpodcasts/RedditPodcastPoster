using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryResultsRepository
{
    Task Save(DiscoveryResultsDocument discoveryResultsDocument);
    IAsyncEnumerable<DiscoveryResultsDocument> GetAllUnprocessed();
    Task SetProcessed(IEnumerable<Guid> ids);
    Task<DiscoveryResultsDocument?> GetById(Guid documentId);
    IAsyncEnumerable<DiscoveryResultsDocument> GetByIds(IEnumerable<Guid> ids);
}