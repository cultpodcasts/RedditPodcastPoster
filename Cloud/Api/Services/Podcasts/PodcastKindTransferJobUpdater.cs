using Microsoft.Extensions.Logging;
using Api.Models;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Podcasts;

/// <summary>Maps a finished <see cref="IPodcastKindTransferService"/> outcome onto the LookUps job document.</summary>
public interface IPodcastKindTransferJobUpdater
{
    Task MarkRunningAsync(Guid podcastId, CatalogueParentKind targetKind, DateTime enqueuedUtc);

    Task ApplyResultAsync(PodcastKindTransferResult result, DateTime enqueuedUtc);
}

public class PodcastKindTransferJobUpdater(
    IParentKindTransferJobStore jobStore,
    TimeProvider timeProvider,
    ILogger<PodcastKindTransferJobUpdater> logger) : IPodcastKindTransferJobUpdater
{
    public async Task MarkRunningAsync(Guid podcastId, CatalogueParentKind targetKind, DateTime enqueuedUtc)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await jobStore.Save(new ParentKindTransferJob
        {
            PodcastId = podcastId,
            TargetKind = targetKind,
            Phase = ParentKindTransferJobPhase.Running,
            EnqueuedUtc = enqueuedUtc,
            StartedUtc = now
        });
    }

    public async Task ApplyResultAsync(PodcastKindTransferResult result, DateTime enqueuedUtc)
    {
        if (result.ParentId is not { } podcastId || result.TargetKind is not { } targetKind)
        {
            logger.LogWarning(
                "{method}: Transfer result {status} had no parent/target; job document not updated.",
                nameof(ApplyResultAsync),
                result.Status);
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var existing = await jobStore.Get(podcastId);
        var job = new ParentKindTransferJob
        {
            PodcastId = podcastId,
            TargetKind = targetKind,
            EnqueuedUtc = existing?.EnqueuedUtc ?? enqueuedUtc,
            StartedUtc = existing?.StartedUtc ?? now,
            CompletedUtc = now,
            PlayableCount = result.PlayableCount,
            FailureIndexingPlayables = result.FailureIndexingPlayables,
            Phase = result.Status switch
            {
                PodcastKindTransferStatus.Accepted => ParentKindTransferJobPhase.Completed,
                PodcastKindTransferStatus.Incomplete => ParentKindTransferJobPhase.Incomplete,
                PodcastKindTransferStatus.AlreadyRunning => ParentKindTransferJobPhase.Running,
                PodcastKindTransferStatus.NotFound => ParentKindTransferJobPhase.Failed,
                PodcastKindTransferStatus.Conflict => ParentKindTransferJobPhase.Failed,
                PodcastKindTransferStatus.InvalidTarget => ParentKindTransferJobPhase.Failed,
                PodcastKindTransferStatus.Failed => ParentKindTransferJobPhase.Failed,
                _ => ParentKindTransferJobPhase.Failed
            },
            FailureReason = result.Status switch
            {
                PodcastKindTransferStatus.Accepted or PodcastKindTransferStatus.Incomplete
                    or PodcastKindTransferStatus.AlreadyRunning => null,
                PodcastKindTransferStatus.NotFound => "Podcast not found.",
                PodcastKindTransferStatus.Conflict => "Transfer conflict.",
                PodcastKindTransferStatus.InvalidTarget => "Invalid target kind.",
                PodcastKindTransferStatus.Failed => "Transfer failed.",
                _ => "Unexpected transfer status."
            }
        };

        await jobStore.Save(job);
    }
}
