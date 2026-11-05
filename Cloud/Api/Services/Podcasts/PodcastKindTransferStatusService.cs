using Api.Models;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Podcasts;

/// <summary>
/// Query-only status for a podcast kind transfer. Prefers a live single-flight marker (Running), then the
/// LookUps job document, then derives Completed when the podcast is gone and a same-id TV/News parent exists.
/// </summary>
public class PodcastKindTransferStatusService(
    IPodcastRepository podcastRepository,
    ITvShowRepository tvShowRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    IParentKindTransferLockStore lockStore,
    IParentKindTransferJobStore jobStore,
    TimeProvider timeProvider) : IPodcastKindTransferStatusService
{
    public async Task<PodcastKindTransferJobStatusResult> GetAsync(
        Guid podcastId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var marker = await lockStore.TryGet(podcastId);
        if (marker is not null && now - marker.LastAliveUtc < PodcastKindTransferService.LockStaleAfter)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new PodcastKindTransferJobStatusResult(
                PodcastKindTransferJobQueryStatus.Running,
                podcastId,
                marker.TargetKind,
                StartedUtc: marker.StartedUtc);
        }

        var job = await jobStore.Get(podcastId);
        if (job is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return FromJob(job);
        }

        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast is not null)
        {
            return new PodcastKindTransferJobStatusResult(
                PodcastKindTransferJobQueryStatus.Idle, podcastId);
        }

        var tvShow = await tvShowRepository.GetTvShow(podcastId);
        if (tvShow is not null)
        {
            return new PodcastKindTransferJobStatusResult(
                PodcastKindTransferJobQueryStatus.Completed,
                podcastId,
                CatalogueParentKind.TvShow);
        }

        var news = await newsOrganisationRepository.GetNewsOrganisation(podcastId);
        if (news is not null)
        {
            return new PodcastKindTransferJobStatusResult(
                PodcastKindTransferJobQueryStatus.Completed,
                podcastId,
                CatalogueParentKind.NewsOrganisation);
        }

        return new PodcastKindTransferJobStatusResult(
            PodcastKindTransferJobQueryStatus.NotFound, podcastId);
    }

    /// <summary>
    /// Maps a LookUps job when no fresh lock is held. Job phase Running without a live marker is never
    /// authoritative Running (worker likely crashed) — surface Failed so the curator can re-enqueue.
    /// </summary>
    private static PodcastKindTransferJobStatusResult FromJob(ParentKindTransferJob job)
    {
        if (job.Phase == ParentKindTransferJobPhase.Running)
        {
            return new PodcastKindTransferJobStatusResult(
                PodcastKindTransferJobQueryStatus.Failed,
                job.PodcastId,
                job.TargetKind,
                job.PlayableCount,
                job.FailureIndexingPlayables,
                job.FailureReason ?? "Transfer marker expired while job was Running.",
                job.EnqueuedUtc,
                job.StartedUtc,
                job.CompletedUtc);
        }

        return new(
            job.Phase switch
            {
                ParentKindTransferJobPhase.Queued => PodcastKindTransferJobQueryStatus.Queued,
                ParentKindTransferJobPhase.Completed => PodcastKindTransferJobQueryStatus.Completed,
                ParentKindTransferJobPhase.Failed => PodcastKindTransferJobQueryStatus.Failed,
                ParentKindTransferJobPhase.Incomplete => PodcastKindTransferJobQueryStatus.Incomplete,
                _ => PodcastKindTransferJobQueryStatus.Idle
            },
            job.PodcastId,
            job.TargetKind,
            job.PlayableCount,
            job.FailureIndexingPlayables,
            job.FailureReason,
            job.EnqueuedUtc,
            job.StartedUtc,
            job.CompletedUtc);
    }
}
