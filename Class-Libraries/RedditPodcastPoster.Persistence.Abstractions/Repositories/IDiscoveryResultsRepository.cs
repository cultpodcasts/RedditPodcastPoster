using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IDiscoveryResultsRepository : IRepository<DiscoveryResultsDocument>
{
    IAsyncEnumerable<DiscoveryResultsDocument> GetAllUnprocessed();
    Task SetProcessed(IEnumerable<Guid> ids);
    Task<DiscoveryResultsDocument?> GetById(Guid documentId);
    IAsyncEnumerable<DiscoveryResultsDocument> GetByIds(IEnumerable<Guid> ids);

    /// <summary>
    /// Max <c>discoveryBegan</c> over Discovery documents still in Cosmos (including processed).
    /// </summary>
    Task<DateTime?> GetLatestDiscoveryBegan(CancellationToken cancellationToken = default);
}
