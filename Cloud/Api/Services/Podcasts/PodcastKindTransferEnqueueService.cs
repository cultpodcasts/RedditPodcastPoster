using Microsoft.Extensions.Logging;
using Api.Models;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Podcasts;

/// <summary>
/// Accepts a kind-transfer command: validates the podcast and target, refuses when a fresh single-flight
/// marker is held, writes a LookUps job in <see cref="ParentKindTransferJobPhase.Queued"/>, and enqueues
/// work for the queue worker. The HTTP request returns before the move runs.
/// </summary>
public class PodcastKindTransferEnqueueService(
    IPodcastRepository podcastRepository,
    IParentKindTransferLockStore lockStore,
    IParentKindTransferJobStore jobStore,
    IPodcastKindTransferQueue queue,
    TimeProvider timeProvider,
    ILogger<PodcastKindTransferEnqueueService> logger) : IPodcastKindTransferEnqueueService
{
    public async Task<PodcastKindTransferEnqueueResult> EnqueueAsync(
        Guid podcastId,
        PodcastKindTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TargetKind is not { } targetKind ||
            targetKind is not (CatalogueParentKind.TvShow or CatalogueParentKind.NewsOrganisation))
        {
            return new PodcastKindTransferEnqueueResult(PodcastKindTransferEnqueueStatus.InvalidTarget);
        }

        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast is null)
        {
            return new PodcastKindTransferEnqueueResult(
                PodcastKindTransferEnqueueStatus.NotFound, podcastId, targetKind);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var marker = await lockStore.TryGet(podcastId);
        if (marker is not null && now - marker.LastAliveUtc < PodcastKindTransferService.LockStaleAfter)
        {
            return new PodcastKindTransferEnqueueResult(
                PodcastKindTransferEnqueueStatus.AlreadyRunning, podcastId, marker.TargetKind);
        }

        var job = new ParentKindTransferJob
        {
            PodcastId = podcastId,
            TargetKind = targetKind,
            Phase = ParentKindTransferJobPhase.Queued,
            EnqueuedUtc = now
        };

        try
        {
            await jobStore.Save(job);
            await queue.EnqueueAsync(
                new PodcastKindTransferQueueMessage
                {
                    PodcastId = podcastId,
                    TargetKind = targetKind,
                    EnqueuedUtc = now
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{method}: Failed to enqueue kind transfer for podcast '{podcastId}' to {targetKind}.",
                nameof(EnqueueAsync),
                podcastId,
                targetKind);
            return new PodcastKindTransferEnqueueResult(
                PodcastKindTransferEnqueueStatus.Failed, podcastId, targetKind);
        }

        return new PodcastKindTransferEnqueueResult(
            PodcastKindTransferEnqueueStatus.Queued, podcastId, targetKind, now);
    }
}
