using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IDiscoverySubmissionsRepository
{
    Task Save(DiscoverySubmission submission, CancellationToken cancellationToken = default);
    /// <summary>Deletes a submission; no-op when it does not exist.</summary>
    Task Delete(Guid id, CancellationToken cancellationToken = default);
    Task<DiscoverySubmission?> GetById(Guid id, CancellationToken cancellationToken = default);
    IAsyncEnumerable<DiscoverySubmission> GetByStatus(DiscoverySubmissionStatus status, CancellationToken cancellationToken = default);
}
