using Api.Models;
using RedditPodcastPoster.Models.Discovery;

namespace Api.Services.Discovery;

public interface IDiscoveryResultsService
{
    Task<DiscoveryCurationData> Get(bool includeHidden, CancellationToken c);
    Task MarkAsProcessed(Guid[] documentIds, Guid[] acceptedResultIds, Guid[] erroredResultIds);
    Task<IEnumerable<DiscoveryResult>> GetDiscoveryResult(DiscoverySubmitRequest discoverySubmitRequest);
    Task UpdateDiscoveryInfoContent();

    /// <summary>
    /// Claims reports for an unsupervised submit by moving each Unprocessed -> Processed (ETag-guarded) before
    /// anything is ingested. All-or-nothing: on any failure the reports already claimed are released.
    /// Returns the ids that could not be claimed (empty on success).
    /// </summary>
    Task<IReadOnlyList<Guid>> TryClaimForProcessing(Guid[] documentIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiscoveryResultsDocument>> GetDocuments(IEnumerable<Guid> documentIds);
}
