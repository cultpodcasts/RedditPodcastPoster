using Api.Models;
using RedditPodcastPoster.Models.Discovery;

namespace Api.Services.Discovery;

public interface IDiscoveryResultsService
{
    Task<DiscoveryCurationData> Get(bool includeHidden, CancellationToken c);

    /// <summary>
    /// Stamps each result Accepted / AcceptError / Rejected and copies podcast/episode ids (and error text)
    /// from <paramref name="outcomes"/> so a lost HTTP response can still be reconciled.
    /// </summary>
    Task MarkAsProcessed(Guid[] documentIds, Guid[] acceptedResultIds, Guid[] erroredResultIds,
        IReadOnlyList<DiscoverySubmitItemOutcome>? outcomes = null);

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

    /// <summary>On Processed reports, sets only the given results to Accepted / AcceptError (retry outcome).</summary>
    Task UpdateResultStates(Guid[] documentIds, Guid[] succeededResultIds, Guid[] erroredResultIds,
        IReadOnlyList<DiscoverySubmitItemOutcome>? outcomes = null);

    /// <summary>
    /// Reads stamped submit outcomes for the given document + result ids (including Processed reports).
    /// Missing stamps mean the item is still pending / in flight.
    /// </summary>
    Task<IReadOnlyList<DiscoverySubmitOutcomeItem>> GetSubmitOutcome(Guid[] documentIds, Guid[] resultIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, DiscoveryPodcastInfo>> GetPodcastInfo(IEnumerable<DiscoveryResult> results,
        CancellationToken c);

    Task<IReadOnlyList<DiscoveryResultsDocument>> GetDocuments(IEnumerable<Guid> documentIds);
}
