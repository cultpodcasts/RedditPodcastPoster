using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IDiscoveryResultsRepository : IRepository<DiscoveryResultsDocument>
{
    IAsyncEnumerable<DiscoveryResultsDocument> GetAllUnprocessed();
    IAsyncEnumerable<DiscoveryResultsDocument> GetAllPendingSupervisor();
    Task SetProcessed(IEnumerable<Guid> ids);

    /// <summary>
    /// Optimistically moves a report from <paramref name="from"/> to <paramref name="to"/> (ETag-guarded).
    /// Returns false when the report is missing, not in <paramref name="from"/>, or changed concurrently.
    /// </summary>
    Task<bool> TryTransitionState(Guid documentId, DiscoveryResultsDocumentState from,
        DiscoveryResultsDocumentState to, CancellationToken cancellationToken = default);
    Task<DiscoveryResultsDocument?> GetById(Guid documentId);
    IAsyncEnumerable<DiscoveryResultsDocument> GetByIds(IEnumerable<Guid> ids);

    /// <summary>
    /// Max <c>discoveryBegan</c> over Discovery documents still in Cosmos (including processed).
    /// </summary>
    Task<DateTime?> GetLatestDiscoveryBegan(CancellationToken cancellationToken = default);
}
