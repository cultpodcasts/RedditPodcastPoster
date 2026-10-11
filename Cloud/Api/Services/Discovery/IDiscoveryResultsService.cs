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
    /// <summary>Releases claimed reports Processed -> Unprocessed (guarded) when nothing was ingested.</summary>
    Task ReleaseClaim(Guid[] documentIds);

    Task<IReadOnlyList<Guid>> TryClaimForProcessing(Guid[] documentIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supervisor apply only: claims held reports by moving each PendingSupervisor -> Processed (ETag-guarded)
    /// before anything is ingested. All-or-nothing; returns the ids that could not be claimed (empty on success).
    /// </summary>
    Task<IReadOnlyList<Guid>> TryClaimHeldForProcessing(Guid[] documentIds, CancellationToken cancellationToken = default);

    /// <summary>Returns claimed reports Processed -> PendingSupervisor after a failed supervisor apply.</summary>
    Task ReleaseHeldClaim(Guid[] documentIds);

    Task<IReadOnlyDictionary<Guid, DiscoveryPodcastInfo>> GetPodcastInfo(IEnumerable<DiscoveryResult> results,
        CancellationToken c);

    Task<IReadOnlyList<DiscoveryResultsDocument>> GetDocuments(IEnumerable<Guid> documentIds);
}
