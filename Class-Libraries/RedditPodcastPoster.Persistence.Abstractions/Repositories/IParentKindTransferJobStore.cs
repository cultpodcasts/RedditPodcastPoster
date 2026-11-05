using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// Latest kind-transfer job state per source podcast (LookUps). Complements the single-flight
/// <see cref="IParentKindTransferLockStore"/> marker used while a transfer is executing.
/// </summary>
public interface IParentKindTransferJobStore
{
    Task<ParentKindTransferJob?> Get(Guid podcastId);

    Task Save(ParentKindTransferJob job);
}
