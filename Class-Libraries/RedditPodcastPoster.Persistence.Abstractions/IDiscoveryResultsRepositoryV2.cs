using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IDiscoveryResultsRepositoryV2
{
    Task Save(DiscoveryResultsDocument discoveryResultsDocument);
    IAsyncEnumerable<DiscoveryResultsDocument> GetAll();
    IAsyncEnumerable<DiscoveryResultsDocument> GetAllUnprocessed();
    Task SetProcessed(IEnumerable<Guid> ids);
    Task<DiscoveryResultsDocument?> GetById(Guid documentId);
    IAsyncEnumerable<DiscoveryResultsDocument> GetByIds(IEnumerable<Guid> ids);
}
